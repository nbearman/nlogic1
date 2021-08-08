//read constant from memory
//first, load address of label "const" as address into RBASE
IADF RBASE
SKIP PC
:const

//then read the value at address of value at address with label "const"
RMEM ALUA

//store an arbitrary value at an arbitrary address
3D WBASE
42 WMEM
//direct read the contents at that address into a register
DMEM3D ALUB
DMEM3D ALUB

//set the ALU to add and store the result
1 ALUM
ALUR GPA

//halt the processor
7F FLAG

//fill with some blank memory before the constant
00 00 00 00 00 00 00 00 00 00

//place a constant value at a named address for example of loading constant in program by label name
@const
00 00 12 34