//=========================================================================
// kernel entry point
//=========================================================================

FILL170 //PC before virtual addressing was enabled

//set the MMU VA break point
IADF WBASE
SKIP PC
00 00 10 00 //MMIO base address (in VA)
08 WOFST //breakpoint register
00 WMEM //breakpoint at 0

//enable the breakpoint
14 WOFST //breakpoint enabled register
01 WMEM //non-zero -> enabled

00 RBASE
00 ROFST

//jump to 0 in user space
00 PC

//=========================================================================
// interrupt handler
//=========================================================================
FILL200
WBASE DMEM00
WOFST DMEM04

//dump registers to kernel's stack
IADF WBASE
SKIP PC
::KERNEL_STACK
00 WOFST
GPA WMEM
04 WOFST
GPB WMEM
08 WOFST
GPC WMEM
0C WOFST
GPD WMEM
10 WOFST
GPE WMEM
14 WOFST
GPF WMEM
18 WOFST
GPG WMEM
1C WOFST
GPH WMEM

20 WOFST
COMPA WMEM
24 WOFST
COMPB WMEM
28 WOFST
RBASE WMEM
2C WOFST
ROFST WMEM
30 WOFST
ALUM WMEM
34 WOFST
ALUA WMEM
38 WOFST
ALUB WMEM
3C WOFST
FPUM WMEM
40 WOFST
FPUA WMEM
44 WOFST
FPUB WMEM
48 WOFST
DMEM00 WMEM
4C WOFST
DMEM04 WMEM

//=========================
// interrupt handler stack layout / local variables
//==========
// 0x00 |   uint    |   FLAG
// 0x04 |   uint    |   faulted PTE
// 0x08 |   uint    |   faulted virtual address
// 0x0C |   uint    |   faulted PTE disk block
// 0x10 |   uint    |   virtual page number from faulted virtual address
// 0x14 |   uint    |   active process ID
// 0x18 |   uint    |   active process page directory physical page
// 0x1C |   uint    |   physical page new page is moved into
//=========================

//add stack frame; registers are dumped to bottom of the stack, so we don't need to save a frame pointer
//(just set WBASE to 00 when it's time to retrieve them)
01 ALUM //add
WBASE ALUA
WOFST ALUB
ALUR WBASE
20 WOFST //20 == size of stack frame, described above

//store FLAG in local variable
    WBASE RBASE
    00 ROFST //FLAG local variable address (from stack layout)
    FLAG RMEM

//determine the cause of the interrupt
    //mask flag to check if interrupt was raised on MMU channel
    FLAG ALUA
    IADF ALUB
    SKIP PC
    00 00 00 01 //mask for the first channel (MMU)
    08 ALUM //AND mode
    ALUR COMPA
    00 COMPB //if flag channel is 1, jump to mmu interrupt handler
    COMPR PC
    :non_mmu_interrupt
    :mmu_interrupt

@non_mmu_interrupt
//if interrupt is not from MMU
    //do nothing if the interrupt is from anywhere besides the MMU
    7F FLAG

@mmu_interrupt
//else interrupt is from MMU
//retrieve the faulted PTE from the MMU
    IADF RBASE
    SKIP PC
    00 00 10 00
    0C ROFST
    RMEM GPA

//retrieve the faulted address from the MMU
    10 ROFST
    RMEM GPB

//store faulted PTE and faulted virtual address in local variables
    WBASE RBASE
    04 ROFST
    GPA RMEM
    08 ROFST
    GPB RMEM

//get the active process ID and page directory physical page from kernel memory
    00 ROFST
    IADF RBASE
    SKIP PC
    ::ACTIVE_PROCESS_ID
    RMEM GPC
    IADF RBASE
    SKIP PC
    ::ACTIVE_PROCESS_PAGE_DIRECTORY_PHYSICAL_PAGE
    RMEM GPD

//store active process ID and page directory physical page in local variables
    WBASE RBASE
    14 ROFST
    GPC RMEM
    18 ROFST
    GPD RMEM

//check PTE protections
//get them from PTE
    GPA ALUA //PTE to ALU
    IADF ALUB //mask to ALU
    SKIP PC
    C0 00 00 00 //mask for the RW bits of the PTE
    08 ALUM //AND mode

    ALUR ALUA //RW bits to ALU
    1E ALUB //shift 30 bits right
    06 ALUM //right shift mode

//see which of 00, 01, 10, 11 the RW bits are
ALUR COMPA
00 COMPB
COMPR PC
:r0w0
:_r0w0
@_r0w0
01 COMPB
COMPR PC
:r0w1
:_r0w1
@_r0w1
02 COMPB
COMPR PC
:r1w0
:_r1w0
@_r1w0
7F FLAG //halt; RW was 11, no page fault should have occurred

@r0w0
//not mapped (possibly syscall)
7F FLAG //TODO for now, just halt

@r1w0
//page is readable but not writable
//either a shared page or a clean page
//page needs to be split or marked as dirty
7F FLAG //TODO for now, just halt

@r0w1
//not readable and "writable" indicates the page is mapped but paged out
//page not resident

//push function address onto stack
IADF WMEM
SKIP PC
::get_open_physical_page
24 WOFST //size of stack frame + 4
RTRN LINK
IADN PC
::FUNC

//result of function call is target physical page number
//pop result from stack
    20 WOFST //20 == size of stack frame, top of stack
    WMEM GPH
//store in local variable



//load page from disk
//get disk block from PTE
    GPA ALUA //PTE to ALU
    IADF ALUB //mask to ALU
    SKIP PC
    00 0F FF FF
    08 ALUM //AND mode
    ALUR GPE
//get virtual page / virtual directory number from virtual address
    GPB ALUA //virtual address to ALU
    IADF ALUB //mask to ALU
    SKIP PC
    FF FF F0 00 //remove physical offset part of virtual address
    ALUR GPF

//store disk block and virtual page number in local variables
    WBASE RBASE
    0C ROFST
    GPE RMEM
    10 ROFST
    GPF RMEM

//load from disk
    //point RMEM to virtual disk
        IADF RBASE
        SKIP PC
        00 00 10 1C //MMIO starts at VA 1000
    //tell disk the target physical page (stored in GPH)
        00 ROFST
        GPH RMEM
    //tell disk target disk block (stored in GPE)
        04 ROFST
        GPE RMEM
    //use read mode
        08 ROFST
        00 RMEM
    //initiate transfer from disk to memory
        0C ROFST
        01 RMEM



//update physical page map
    //point RMEM to physical page map array
        IADF RBASE
        SKIP PC
        ::physical_page_map

    //calculate offset of target physical page entry
        02 ALUM //multiply
        GPH ALUA //target physical page
        14 ALUB //20 bytes per entry
        ALUR ROFST //physical page map offset

    GPC RMEM //set process ID to user process

    //move to next field in entry
        01 ALUM //add
        ROFST ALUA
        04 ALUB
        ALUR ROFST

    GPD RMEM //set directory physical page to user directory

    //move to next field
        ALUR ALUA
        ALUR ROFST
        
    GPF RMEM //set virtual page/directory number to that of the faulted address

    //move to next field
        ALUR ALUA
        ALUR ROFST

    01 RMEM //only one process references this page

    //move to next field
        ALUR ALUA
        ALUR ROFST

    GPE RMEM //store origin disk block as the disk block


//update process map
    //point RMEM to process map
        IADF RBASE
        SKIP PC
        ::process_map

    //calculate offset of target process entry ((process ID - 1) is index into map)
        // (16 * (PID - 1)) + 8
        03 ALUM //subtract
        GPC ALUA //process ID
        01 ALUB //minus 1
        ALUR ALUA //ALUA = PID - 1

        10 ALUB //16 byte per entry
        02 ALUM //multiply
        ALUR ALUA //process entry map offset
        08 ALUB //offset into entry for number of resident pages
        01 ALUM //add
        ALUR ROFST //total offset into process map

    //increment number of resident pages for this process
        RMEM ALUA
        01 ALUB
        ALUR RMEM

//update page table
//TODO how do we know if we loaded a page table or a leaf page?
//  if we loaded a leaf page, we need to update the page table
//  but if we loaded a page table, we need to update the page directory
//  traverse the page table: the first PDE/PTE with matching protection bits
//  was the one that failed

//get PDE
    05 ALUM //left shift
    GPD ALUA //active process page directory physical page
    0C ALUB //12 bits
    ALUR GPA //GPA = page directory base address

    //page table number = (virtual address & 0xFFC00000) >> 22
    08 ALUM //AND
    GPB ALUA //virtual address
    IADF ALUB //mask for first 12 bits
    SKIP PC
    FF C0 00 00

    ALUR ALUA
    06 ALUM //right shift
    14 ALUB //20 bits (page number is given by shifting 22 bits, but offset into page table is page number * 4, so shift left 2 bits)

    ALUR ALUB
    GPA ALUA
    07 ALUM //OR

    01 ALUM //add
    ALUR ALUA //page directory entry physical address
    IADF ALUB //add 0x00 3F 00 00 to get kernel virtual address of physical address
    SKIP PC
    00 3F 00 00

    ALUR RBASE
    00 ROFST

//TODO keep following page table to find faulted PTE

//return from interrupt


//nothing left
02 FPUM
11 FPUA
22 FPUB
FPUR GPC
7F FLAG

FILL600
@@physical_page_map
//physical page map

//=========================
// physical page mapping entry
//==========
// 0x00 |   uint    |   process id
// 0x04 |   uint    |   directory physical page
// 0x08 |   uint    |   virtual page number
// 0x0C |   uint    |   number of references
// 0x10 |   uint    |   disk block number
//=========================

//boot sequence (no owner)
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00

//kernel page directory
00 00 00 01 //kernel process ID == 1
00 00 00 01 //owning page directory is this directory, kernel's page directory
00 00 00 00 //this is a directory
00 00 00 01 //kernel process references this physical page
00 00 00 00 //no disk block number, can never be evicted

//kernel page table 0
00 00 00 01 //kernel process ID == 1
00 00 00 01 //owning page directory is kernel directory
00 00 00 00 //virtual table 0
00 00 00 01 //kernel process references this physical page
00 00 00 00 //no disk block number, can never be evicted

//kernel virtual page 0
00 00 00 01 //kernel process ID == 1
00 00 00 01 //owning page directory is kernel directory
00 00 00 00 //virtual page 0
00 00 00 01 //kernel process references this physical page
00 00 00 00 //no disk block number, can never be evicted

//empty (no owner)
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00

//user page directory
00 00 00 02 //user process ID == 2
00 00 00 05 //owning page directory is this directory, user's page directory
00 00 00 00 //this is a directory
00 00 00 01 //user process references this physical page
00 00 00 00 //no disk block number yet

//user page table 0
00 00 00 02 //user process ID == 2
00 00 00 05 //owning page directory is user page directory
00 00 00 00 //virtual table 0
00 00 00 01 //user process references this physical page
00 00 00 00 //no disk block number yet

//user virtual page 0
00 00 00 02 //user process ID == 2
00 00 00 05 //owning page directory is user page directory
00 00 00 00 //virtual page 0
00 00 00 01 //user process references this physical page
00 00 00 64 //loaded from disk block 100



//empty (no owner)
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
//empty (no owner)
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
//empty (no owner)
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
//empty (no owner)
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
//empty (no owner)
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
//empty (no owner)
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
//empty (no owner)
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
//empty (no owner)
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00
00 00 00 00

//end physical page map (16 page mappings)
FILL840
@@process_map
//process map

//=========================
// process map entry
//==========
// 0x00 |   uint    |   process id
// 0x04 |   uint    |   number of mapped virtual pages
// 0x08 |   uint    |   number of pages resident in memory
// 0x0C |   uint    |   disk block of process page directory
//=========================

//kernel process descriptor
00 00 00 01 //kernel process ID == 1
00 00 00 03 //3 pages are mapped: 2 pages of memory and 1 mapped to the MMU
00 00 00 03 //page directory, page table, 1 page of memory
00 00 00 00 //no disk block number, kernel page directory can never be evicted

//user process descriptor
00 00 00 01 //user process ID == 2
00 00 00 03 //2 pages are mapped: 2 pages of memory
00 00 00 03 //page directory, page table, 1 page of memory
00 00 00 00 //TODO figure out if we're supposed to load process page directory from disk...
            //(it should probably be built dynamically from some kind of description file
            // that the kernel can read to determine how many pages of the program are
            // mapped out of the box [length of program data])

//end process map (16 process descriptors)
FILL940


//=========================================================================
// [function] get_open_physical_page | void
//=========================================================================
@@get_open_physical_page
//returns physical page number that is available for incoming page
//may or may not result in page eviction


//calculate address where return value should be stored
    03 ALUM //subtract
    WBASE ALUA //original FP
    54 ALUB // -84
    ALUR GPH //GPH = result address = FP - 84

//look for open pages, which we can use without evicting anything
//open pages have a process ID of 0

//iterate over physical page map
    //point RMEM to physical page map array
        IADF RBASE
        SKIP PC
        ::physical_page_map
        00 ROFST

    00 GPA //GPA = index
    10 GPB //GPB = max_index

    @open_page_loop
    //check if we're at the end of the loop (index == 16?)
        GPA COMPA
        GPB COMPB
        COMPR PC
        :open_page_loop_end
        :open_page_loop_go

    @open_page_loop_go
    //calculate offset from index
        02 ALUM //multiply mode
        GPA ALUA // ALUA = index
        14 ALUB //20 bytes per entry
        ALUR ROFST

    //read process ID (offset 0)
    RMEM GPC //GPC = process id

    //process id == 0?
        GPC COMPA
        00 COMPB
        COMPR PC
        :open_page_proc_id_0
        :open_page_loop_next
    
    @open_page_proc_id_0
    // found an open page
    // return the index as the open physical page
        WBASE ALUA //store FP somewhere
        GPH WBASE //point WMEM to the result address
        00 WOFST
        GPA WMEM //result = index
        ALUA WBASE //restore FP
        LINK PC //return

    

    @open_page_loop_next
    //increment index
        01 ALUM //add mode
        GPA ALUA
        01 ALUB
        ALUR GPA //index += 1
    //go to start of loop
    IADN PC
    :open_page_loop

    //TODO implement this


@open_page_loop_end
//no open pages
BREAK


//TODO implement this
00 RBASE
00 ROFST
7F RMEM

//return
LINK PC
