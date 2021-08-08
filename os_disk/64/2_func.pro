@@FUNC
/////////////////////////////////////////////////
//Call target function with new stack frame
//Processor state is preserved and restored
//before returning to caller

//push target function arguments onto stack
//push target function address onto stack
//load return address into LINK
//jump to FUNC
//WMEM accessor is reserved for stack operations
//DMEM will be overwritten
//target function address will be overwritten with
//function call result

//target function should store result at (FP - 0x54 (84))
//first function argument is accessible at (FP - 0x58 (88))

//invoke this helper with:
//RTRN LINK
//IADN PC
//::FUNC
/////////////////////////////////////////////////
//caller save layout
//GPA
//GPB
//GPC
//GPD
//GPE
//GPF
//GPG
//GPH
//ALUM
//ALUA
//ALUB
//FPUM
//FPUA
//FPUB
//RBASE
//ROFST
//COMPA
//COMPB
//LINK
//frame pointer
/////////////////////////////////////////////////

//clear ALU
ALUM DMEM00
ALUA DMEM04
ALUB DMEM08

//clear GPA to store target address
GPA DMEM0C

//set up ALU for popping target function address
03 ALUM //subtract mode
WOFST ALUA
04 ALUB //4 bytes per register


// STACK ////////////////////////////////////////
// ... local variables      <--- FP
// arg2
// arg1
// arg0
// target function address
//                          <--- SP
//WBASE currently holds caller FP
//WOFST currently holds caller SP

//read top of stack: target function address
ALUR WOFST //SP = SP - 4
WMEM GPA

//restore SP to top of stack
ALUA WOFST //SP = SP

//set up ALU for stacking
01 ALUM //add mode

//push caller save registers onto stack
DMEM0C WMEM //GPA was stored in DMEM earlier
ALUR WOFST
ALUR ALUA
GPB WMEM
ALUR WOFST
ALUR ALUA
GPC WMEM
ALUR WOFST
ALUR ALUA
GPD WMEM
ALUR WOFST
ALUR ALUA
GPE WMEM
ALUR WOFST
ALUR ALUA
GPF WMEM
ALUR WOFST
ALUR ALUA
GPG WMEM
ALUR WOFST
ALUR ALUA
GPH WMEM
ALUR WOFST
ALUR ALUA

//ALU was stored in DMEM temporarily
DMEM00 WMEM
ALUR WOFST
ALUR ALUA
DMEM04 WMEM
ALUR WOFST
ALUR ALUA
DMEM08 WMEM
ALUR WOFST
ALUR ALUA

FPUM WMEM
ALUR WOFST
ALUR ALUA
FPUA WMEM
ALUR WOFST
ALUR ALUA
FPUB WMEM
ALUR WOFST
ALUR ALUA

RBASE WMEM
ALUR WOFST
ALUR ALUA
ROFST WMEM
ALUR WOFST
ALUR ALUA

COMPA WMEM
ALUR WOFST
ALUR ALUA
COMPB WMEM
ALUR WOFST
ALUR ALUA

LINK WMEM
ALUR WOFST
ALUR ALUA

//push frame pointer on to stack
WBASE WMEM
ALUR WOFST

//add a stack frame
WBASE ALUA
WOFST ALUB
ALUR WBASE
00 WOFST

SKIP LINK
GPA PC
00 00 //NOP so SKIP points to the correct address

//return from target function

//retrieve frame pointer from stack
//subtract 4 from the current frame pointer to
//get the last item on the stack (the last FP)
WBASE ALUA
04 ALUB
03 ALUM //subtract mode
ALUR WBASE //set WBASE to last stack slot
00 WOFST //(clear WOFST)
WMEM WBASE //FP is the last thing in the stack

ALUR ALUA //ALUR still holds the top stack slot, equivalent to (old FP + old SP)
WBASE ALUB
//subtract last FP (WBASE) from last FP + SP (ALUR) to get last SP
ALUR WOFST //store last SP in WOFST
//WBASE now holds old FP and WOFST now holds old SP
//FP and SP are now current

//set up ALU for unstacking
WOFST ALUA //FP to ALU
04 ALUB //ALU is in -4 mode

//pop last FP from the stack, don't store because it's already in WBASE
ALUR WOFST
ALUR ALUA

//pop caller save registers from stack
WMEM LINK
ALUR WOFST
ALUR ALUA

WMEM COMPB
ALUR WOFST
ALUR ALUA
WMEM COMPA
ALUR WOFST
ALUR ALUA

WMEM ROFST
ALUR WOFST
ALUR ALUA
WMEM RBASE
ALUR WOFST
ALUR ALUA

WMEM FPUB
ALUR WOFST
ALUR ALUA
WMEM FPUA
ALUR WOFST
ALUR ALUA
WMEM FPUM
ALUR WOFST
ALUR ALUA

//store ALU in DMEM while we're still using it
WMEM DMEM08 //ALUB
ALUR WOFST
ALUR ALUA
WMEM DMEM04 //ALUA
ALUR WOFST
ALUR ALUA
WMEM DMEM00 //ALUM
ALUR WOFST
ALUR ALUA

WMEM GPH
ALUR WOFST
ALUR ALUA
WMEM GPG
ALUR WOFST
ALUR ALUA
WMEM GPF
ALUR WOFST
ALUR ALUA
WMEM GPE
ALUR WOFST
ALUR ALUA
WMEM GPD
ALUR WOFST
ALUR ALUA
WMEM GPC
ALUR WOFST
ALUR ALUA
WMEM GPB
ALUR WOFST
ALUR ALUA
WMEM GPA

//finished with ALU, restore it from DMEM
//TODO this can be made slightly more efficient by just doing ALU last
DMEM00 ALUM
DMEM04 ALUA
DMEM08 ALUB

//return to the caller
LINK PC
/////////////////////////////////////////////////
