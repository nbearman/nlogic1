using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace nlogic_sim
{
    public class MemoryManagementUnit : MMIO, HardwareInterrupter
    {
        /// <summary>
        /// The size of a physical and virtual pages in bytes using this MMU; 4096 bytes (4 kilobytes)
        /// </summary>
        public const uint PAGE_SIZE = 0x1000;

        /// <summary>
        /// The base address in physical memory of the active process's page directory
        /// </summary>
        private uint active_page_directory_base_address;

        /// <summary>
        /// The base address in physical memory of the page directory to switch to when the
        /// MMU breakpoint is next hit
        /// </summary>
        private uint queued_page_directory_base_address;

        /// <summary>
        /// When enabled (breakpoint_enabled), a virtual address which, when the MMU is next instructed to access,
        /// will exit kernel mode and swap to the queued page directory
        /// </summary>
        private uint mmu_breakpoint;

        /// <summary>
        /// The page table entry or page directory entry which caused the MMU to fault due to the state
        /// of its protection bits. Stored for reference by the page fault or syscall handlers
        /// </summary>
        private uint faulted_pte;

        /// <summary>
        /// The virtual address that caused the MMU to fault. Stored for reference by the page fault or
        /// syscall handlers
        /// </summary>
        private uint faulted_address;

        /// <summary>
        /// An MMU register controlling the function of the MMU. If true, the MMU will attempt to
        /// translate addresses as usual. If false, the MMU will pass all addresses through without
        /// translation and without raising any faults or interrupts.
        /// The MMU should start disabled; the boot sequence of the kernel can set up its memory using
        /// physical addresses directly, then enable the MMU by writing to this register.
        /// </summary>
        private bool enabled = false;

        /// <summary>
        /// The MMU receives a signal when the processor cycles in order to know
        /// when to clear this faulted state. Currently occurs in: SimulationEnvironment.run(),
        /// immediately before cycling
        /// </summary>
        private bool faulted = false;

        /// <summary>
        /// When in kernel mode, the MMU will not raise interrupts
        /// </summary>
        private bool kernel_mode = false;

        /// <summary>
        /// When the MMU breakpoint is enabled, the breakpoint address will be checked for during translation. If
        /// disabled, the breakpoint will not be triggered.
        /// </summary>
        private bool breakpoint_enabled = false;

        /// <summary>
        /// Physical memory of this MMU's environment (RAM)
        /// </summary>
        private byte[] environment_memory;

        /// <summary>
        /// Callback supplied by the environment to send an interrupt to the processor.
        /// Parameters:
        ///     (bool): is this a retry interrrupt
        ///     (bool): is this a kernel-only interrupt
        /// </summary>
        private Action<bool, bool> raise_interrupt;
        
        public MemoryManagementUnit(byte[] memory)
        {
            environment_memory = memory;
        }

        //simulation environment interface methods

        public bool translate_address_for_preview(uint address, out uint translation)
        {
            if (!this.enabled)
            {
                translation = address;
                return true;
            }

            translation = 0;

            //do not translate any addresses while faulted
            //no reads or writes should occur until the processor cycles and enters the trap
            if (this.faulted)
            {
                return false;
            }

            //proceed with translation


            //get page directory entry from active page directory
            uint page_directory_entry_address = get_page_directory_entry_address(this.active_page_directory_base_address, address);
            uint page_directory_entry_data = read_environment_memory_byte(page_directory_entry_address);
            PageTableEntry PDE = new PageTableEntry(page_directory_entry_data);

            //check that the page table is safe to read
            ProtectionCheckResult protection = check_protection(PDE, false);
            if (protection != ProtectionCheckResult.SAFE)
            {
                return false;
            }

            //get page table entry from page table
            uint page_table_entry_address = get_page_table_entry_address(PDE.number, address);
            uint page_table_entry_data = read_environment_memory_byte(page_table_entry_address);
            PageTableEntry PTE = new PageTableEntry(page_table_entry_data);

            //check that the physical page allows the planned memory operation
            protection = check_protection(PTE, false);
            if (protection != ProtectionCheckResult.SAFE)
            {
                return false;
            }

            //the page table is safe to access
            //get the final physical address
            translation = get_physical_address(PTE.number, address);

            return true;
        }

        /// <summary>
        /// Translates a virtual address into a physical address.
        /// Raises and interrupt and returns false if translation
        /// resulted in a fault.
        /// Returns true if the translation was successful.
        /// Fills translation with the translated address.
        /// </summary>
        /// <param name="address">Virtual address to translate into a physical address</param>
        /// <param name="write">True if the planned memory operation is a write, false if it is a read</param>
        /// <param name="translation">Holds translated physical address if the translation is successful,
        /// 0 otherwise</param>
        /// <returns></returns>
        public bool translate_address(uint address, bool write, out uint translation)
        {
            if (!this.enabled)
            {
                translation = address;
                return true;
            }

            translation = 0;

            //do not translate any addresses while faulted
            //no reads or writes should occur until the processor cycles and enters the trap
            if (this.faulted)
            {
                return false;
            }

            //swap the active page directory and queued page directory if hitting the breakpoint
            if (this.breakpoint_enabled && address == this.mmu_breakpoint)
            {
                this.breakpoint_enabled = false;
                this.swap_directories();
            }


            //proceed with translation


            //get page directory entry from active page directory
            uint page_directory_entry_address = get_page_directory_entry_address(this.active_page_directory_base_address, address);
            uint page_directory_entry_data = read_environment_memory_byte(page_directory_entry_address);
            PageTableEntry PDE = new PageTableEntry(page_directory_entry_data);

            //check that the page table is safe to read
            ProtectionCheckResult protection = check_protection(PDE, false);
            if (protection != ProtectionCheckResult.SAFE)
            {
                //store the PTE whose protection would be violated for reference by the kernel
                this.faulted_pte = PDE.original_pte_data;
                //store the address that caused the the fault
                this.faulted_address = address;

                //raise a kernel-handled interrupt and abort translation
                //set the RETRY flag if the protection requires a retry interrupt
                this.raise_interrupt(protection == ProtectionCheckResult.RETRY_INTERRUPT, true);

                return false;
            }

            //get page table entry from page table
            uint page_table_entry_address = get_page_table_entry_address(PDE.number, address);
            uint page_table_entry_data = read_environment_memory_byte(page_table_entry_address);
            PageTableEntry PTE = new PageTableEntry(page_table_entry_data);

            //set the refernced bit on the PDE for this page table, since we just accessed the page table
            PDE.referenced = true;

            //check that the physical page allows the planned memory operation
            protection = check_protection(PTE, write);
            if (protection != ProtectionCheckResult.SAFE)
            {
                //store the PTE whose protection would be violated for reference by the kernel
                this.faulted_pte = PTE.original_pte_data;
                //store the address that caused the the fault
                this.faulted_address = address;

                //update the page directory entry in memory, since we set the referenced bit
                write_pte_to_environment_memory(PDE, page_directory_entry_address);

                //raise a kernel-handled interrupt and abort translation
                //set the RETRY flag if the protection requires a retry interrupt
                this.raise_interrupt(protection == ProtectionCheckResult.RETRY_INTERRUPT, true);

                return false;
            }

            //set the dirty bit of the page if we are about to write to the page
            if (write)
            {
                PTE.dirty = true;
            }
            //set the refernced bit on the PTE for this page, since we're about to access the page
            PTE.referenced = true;
            write_pte_to_environment_memory(PTE, page_table_entry_address);

            //set the dirty bit for the page directory entry, since we just modified the page table it points to
            PDE.dirty = true;
            write_pte_to_environment_memory(PDE, page_directory_entry_address);


            //the page table is safe to access
            //get the final physical address
            translation = get_physical_address(PTE.number, address);

            return true;
        }

        /// <summary>
        /// Clear the fault status of the MMU
        /// </summary>
        public void clear_fault()
        {
            this.faulted = false;
        }

        public void swap_directories()
        {
            uint swap = this.active_page_directory_base_address;
            this.active_page_directory_base_address = this.queued_page_directory_base_address;
            this.queued_page_directory_base_address = swap;
        }

        //hardware interrupter device interface methods
        void HardwareInterrupter.register_signal_callback(Action<bool, bool> signal_callback)
        {
            this.raise_interrupt = signal_callback;
        }
        
        
        
        //MMIO device interface methods

        uint MMIO.get_size()
        {
            //28 bytes for 7 uint registers:
            //  active page directory base address
            //  queued page directory base address
            //  virtual address mmu breakpoint
            //  faulted PTE
            //  faulted address
            //  breakpoint enabled
            //  enabled
            return 28;
        }

        //TODO this is out of date (missing MMU registers) and currently not used (write_byte / read_byte currently used instead)
        void MMIO.write_memory(uint address, byte[] data)
        {
            uint new_value = Utility.uint32_from_byte_array(data);
            if (address == (uint)MMIOLayout.ACTIVE_PAGE_DIRECTORY_BASE_ADDRESS_REGISTER)
                this.active_page_directory_base_address = new_value;
            else if (address == (uint)MMIOLayout.QUEUED_PAGE_DIRECTORY_BASE_ADDRESS_REGISTER)
                this.queued_page_directory_base_address = new_value;
            else if (address == (uint)MMIOLayout.MMU_DIRECTORY_SWAP_BREAKPOINT_REGISTER)
                this.mmu_breakpoint = new_value;
            else if (address == (uint)MMIOLayout.ENABLED)
                //set the MMU to enabled only if a non-zero value is written
                this.enabled = new_value > 0 ? true : false;
            else if (address == (uint)MMIOLayout.FAULTED_PTE_REGISTER)
                throw new ArgumentException("MMU access error: the faulted PTE register is read-only");
            else if (address == (uint)MMIOLayout.FAULTED_ADDRESS_REGISTER)
                throw new ArgumentException("MMU access error: the faulted address register is read-only");
            else
                throw new ArgumentException("MMU access error: the given address is not a writable register's address");
        }

        //TODO this is out of date (missing MMU registers) and currently not used (write_byte / read_byte currently used instead)
        public byte[] read_memory(uint address, uint length)
        {
            if (length > 4)
                throw new ArgumentException("MMU access error: cannot read more than 4 bytes from an MMU register");
            if (address == (uint)MMIOLayout.ACTIVE_PAGE_DIRECTORY_BASE_ADDRESS_REGISTER)
                return Utility.byte_array_from_uint32(length, this.active_page_directory_base_address);
            else if (address == (uint)MMIOLayout.QUEUED_PAGE_DIRECTORY_BASE_ADDRESS_REGISTER)
                return Utility.byte_array_from_uint32(length, this.queued_page_directory_base_address);
            else if (address == (uint)MMIOLayout.MMU_DIRECTORY_SWAP_BREAKPOINT_REGISTER)
                return Utility.byte_array_from_uint32(length, this.mmu_breakpoint);
            else if (address == (uint)MMIOLayout.FAULTED_PTE_REGISTER)
                return Utility.byte_array_from_uint32(length, this.faulted_pte);
            else if (address == (uint)MMIOLayout.FAULTED_ADDRESS_REGISTER)
                return Utility.byte_array_from_uint32(length, this.faulted_address);
            else if (address == (uint)MMIOLayout.ENABLED)
                return Utility.byte_array_from_uint32(length, this.enabled ? (uint)1 : (uint)0);
            else
                throw new ArgumentException("MMU access error: the given address is not a readable register's address");
        }

        public void write_byte(uint address, byte data)
        {
            //TODO clean this mess up by changing the MMU's registers from uint variables
            //to some kind of dictionary or array or something with accessors to make it convenient
            uint value;
            if (address >= (uint)MMIOLayout.ENABLED + 4)
                throw new ArgumentException("MMU access error: the given address is beyond the range of writable registers");
            else if (address >= (uint)MMIOLayout.ENABLED)
                value = this.enabled ? (uint)1 : (uint)0;
            else if (address >= (uint)MMIOLayout.BREAKPOINT_ENABLED)
                value = this.breakpoint_enabled ? (uint)1 : (uint)0;
            else if (address >= (uint)MMIOLayout.FAULTED_ADDRESS_REGISTER)
                throw new ArgumentException("MMU access error: the faulted address register is read-only");
            else if (address >= (uint)MMIOLayout.FAULTED_PTE_REGISTER)
                throw new ArgumentException("MMU access error: the faulted PTE register is read-only");
            else if (address >= (uint)MMIOLayout.MMU_DIRECTORY_SWAP_BREAKPOINT_REGISTER)
                value = this.mmu_breakpoint;
            else if (address >= (uint)MMIOLayout.QUEUED_PAGE_DIRECTORY_BASE_ADDRESS_REGISTER)
                value = this.queued_page_directory_base_address;
            else if (address >= (uint)MMIOLayout.ACTIVE_PAGE_DIRECTORY_BASE_ADDRESS_REGISTER)
                value = this.active_page_directory_base_address;
            else
                throw new ArgumentException("MMU access error: the given address is not a writable register's address");

            byte[] register_bytes = Utility.byte_array_from_uint32(4, value);
            register_bytes[address % 4] = data;
            uint new_value = Utility.uint32_from_byte_array(register_bytes);

            if (address >= (uint)MMIOLayout.ENABLED)
                this.enabled = (new_value != 0);
            else if (address >= (uint)MMIOLayout.BREAKPOINT_ENABLED)
                this.breakpoint_enabled = (new_value != 0);
            else if (address >= (uint)MMIOLayout.MMU_DIRECTORY_SWAP_BREAKPOINT_REGISTER)
                this.mmu_breakpoint = new_value;
            else if (address >= (uint)MMIOLayout.QUEUED_PAGE_DIRECTORY_BASE_ADDRESS_REGISTER)
                this.queued_page_directory_base_address = new_value;
            else if (address >= (uint)MMIOLayout.ACTIVE_PAGE_DIRECTORY_BASE_ADDRESS_REGISTER)
                this.active_page_directory_base_address = new_value;
            else
                throw new ArgumentException("MMU access error: the given address is not a writable register's address");
        }

        public byte read_byte(uint address)
        {
            uint value;
            if (address >= (uint)MMIOLayout.ENABLED + 4)
                throw new ArgumentException("MMU access error: the given address is beyond the range of readable registers");
            else if (address >= (uint)MMIOLayout.ENABLED)
                value = this.enabled ? (uint)1 : (uint)0;
            else if (address >= (uint)MMIOLayout.BREAKPOINT_ENABLED)
                value = this.breakpoint_enabled ? (uint)1 : (uint)0;
            else if (address >= (uint)MMIOLayout.FAULTED_ADDRESS_REGISTER)
                value = this.faulted_address;
            else if (address >= (uint)MMIOLayout.FAULTED_PTE_REGISTER)
                value = this.faulted_pte;
            else if (address >= (uint)MMIOLayout.MMU_DIRECTORY_SWAP_BREAKPOINT_REGISTER)
                value = this.mmu_breakpoint;
            else if (address >= (uint)MMIOLayout.QUEUED_PAGE_DIRECTORY_BASE_ADDRESS_REGISTER)
                value = this.queued_page_directory_base_address;
            else if (address >= (uint)MMIOLayout.ACTIVE_PAGE_DIRECTORY_BASE_ADDRESS_REGISTER)
                value = this.active_page_directory_base_address;
            else
                throw new ArgumentException("MMU access error: the given address is not a readable register's address");

            byte[] register_bytes = Utility.byte_array_from_uint32(4, value);
            return register_bytes[address % 4];
        }

        /// <summary>
        /// Check if the target page associated with a page table (or page directory)
        /// entry is protected and should therefore fault during the given operation (reading or writing).
        /// </summary>
        /// <param name="pte">Page table entry to check the protection bits on</param>
        /// <param name="write">True if the memory operation to be performed on the target page is a write,
        /// false if the operation is a read.
        /// </param>
        /// <returns>The type of interrupt the MMU should raise if the page is protected, or a safe status if
        /// the planned operation is permitted.</returns>
        private static ProtectionCheckResult check_protection(PageTableEntry pte, bool write)
        {
            if (pte.readable)
            {
                //page is safe to read
                //page fault only the page is not writable and we are writing
                if (!pte.writable && write)
                {
                    //readable but not writable
                    //either a shared physical page or a clean page
                    //page needs to be split or marked as dirty
                    return ProtectionCheckResult.RETRY_INTERRUPT;
                }

                //readable and not writing or
                //readable, writable, and writing
                return ProtectionCheckResult.SAFE;
            }

            //if a page is not "readable," both reads and writes are illegal
            //the writable bit now indicates if the page is paged out or not allocated
            else
            {
                if (pte.writable)
                {
                    //not readable and "writable" indicates the page is mapped but paged out
                    //page is evicted, page fault
                    return ProtectionCheckResult.RETRY_INTERRUPT;
                }
                else
                {
                    //not readable, not writable indicates the page is not mapped at all
                    //the handler will also decide if this was intended to be a system call
                    //access violation, non-retry kernel table interrupt
                    return ProtectionCheckResult.NON_RETRY_INTERRUPT;
                }
            }
        }

        private uint read_environment_memory_byte(uint address)
        {
            byte[] result = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                result[i] = this.environment_memory[address + i];
            }
            return Utility.uint32_from_byte_array(result);
        }

        private void write_pte_to_environment_memory(PageTableEntry pte, uint address)
        {
            byte[] data = Utility.byte_array_from_uint32(4, pte.pte_to_uint());
            for (int i = 0; i < 4; i++)
            {
                environment_memory[address + i] = data[i];
            }

        }

        private static uint get_page_directory_entry_address(uint page_directory_base_address, uint virtual_address)
        {
            //virtual address mask:
            //[directory #] [page #]      [offset]
            //[1111 1111 11][00 0000 0000][0000 0000 0000]
            uint directory_number = (virtual_address & 0xFFC00000) >> 22;

            //page directory entry address:
            //[directory base address]  [directory #] [00]
            //[0000 0000 0000 0000 0000][00 0000 0000][00]
            return (page_directory_base_address << 12) | (directory_number << 2);
        }

        private static uint get_page_table_entry_address(uint page_table_physical_page_number, uint virtual_address)
        {
            //virtual address mask:
            //[directory #] [page #]      [offset]
            //[0000 0000 00][11 1111 1111][0000 0000 0000]
            uint page_number = (virtual_address & 0x003FF000) >> 12;

            //page table entry address:
            //[table phsyical page #]   [page #]      [00]
            //[0000 0000 0000 0000 0000][00 0000 0000][00]
            return (page_table_physical_page_number << 12) | (page_number << 2);
        }

        private static uint get_physical_address(uint physical_page_number, uint virtual_address)
        {
            //virtual address mask:
            //[directory #] [page #]      [offset]
            //[0000 0000 00][00 0000 0000][1111 1111 1111]
            uint offset_into_page = virtual_address & 0x00000FFF;

            //physical address:
            //[physical page #]         [offset]
            //[0000 0000 0000 0000 0000][0000 0000 0000]
            return (physical_page_number << 12) | offset_into_page;
        }

        public void initialize(SimulationEnvironment environment)
        {
            //no set up required
            return;
        }

        private struct PageTableEntry
        {
            //PTE protection bits
            public bool readable;
            public bool writable;
            public bool referenced;
            public bool dirty;

            //number represented by the lower 20 bits of the PTE
            //either a directory number or a physical page number
            public uint number;

            //original uint used to create this PTE
            public uint original_pte_data;

            public PageTableEntry(uint data)
            {
                this.readable = Utility.get_bit(data, (uint)PageTableEntryProtectionBits.READABLE);
                this.writable = Utility.get_bit(data, (uint)PageTableEntryProtectionBits.WRITABLE);
                this.referenced = Utility.get_bit(data, (uint)PageTableEntryProtectionBits.REFERENCED);
                this.dirty = Utility.get_bit(data, (uint)PageTableEntryProtectionBits.DIRTY);

                //physical page number is held in the bottom 20 bits
                this.number = data & 0x000FFFFF;

                //store the original PTE as it was read from; this will allow the MMU to 
                //fill a register with the faulty PTE for the processor to retrieve later
                this.original_pte_data = data;
            }

            /// <summary>
            /// Creates a new uint from this PageTableEntry based on the state of the
            /// bits variables (readable, writable, referenced, dirty) and the number;
            /// data contained in the officailly unused bits of the PTE are lost
            /// </summary>
            /// <returns></returns>
            public uint pte_to_uint()
            {
                uint result = this.number;
                if (this.readable)
                    result = Utility.set_bit(result, (uint)PageTableEntryProtectionBits.READABLE);
                if (this.writable)
                    result = Utility.set_bit(result, (uint)PageTableEntryProtectionBits.WRITABLE);
                if (this.referenced)
                    result = Utility.set_bit(result, (uint)PageTableEntryProtectionBits.REFERENCED);
                if (this.dirty)
                    result = Utility.set_bit(result, (uint)PageTableEntryProtectionBits.DIRTY);
                return result;
            }


        }

        private enum PageTableEntryProtectionBits
        {
            READABLE = 0,
            WRITABLE = 1,
            REFERENCED = 2,
            DIRTY = 3,
        }

        private enum ProtectionCheckResult
        {
            SAFE = 0,
            RETRY_INTERRUPT = 1,
            NON_RETRY_INTERRUPT = 2,
        }

        private enum MMIOLayout
        {
            ACTIVE_PAGE_DIRECTORY_BASE_ADDRESS_REGISTER = 0,
            QUEUED_PAGE_DIRECTORY_BASE_ADDRESS_REGISTER = 4,
            MMU_DIRECTORY_SWAP_BREAKPOINT_REGISTER = 8,
            FAULTED_PTE_REGISTER = 12,
            FAULTED_ADDRESS_REGISTER = 16,
            BREAKPOINT_ENABLED = 20,
            ENABLED = 24,
        }
    }
}