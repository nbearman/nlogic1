//move the write head to the display's base addr
IADF WBASE
SKIP PC
00 01 00 00

//clear the display by writing to the first byte
01 WMEM

//set the offset to the character input address of the display
01 WOFST

//write "AAAA" to the display
IADF WMEM
SKIP PC
41 41 41 41

//write "BBBB" to the display
IADF WMEM
SKIP PC
42 42 42 42

//write "\0\0\0C" to the display
IADF WMEM
SKIP PC
00 00 00 43

//write "D" to the display
44 WMEM

//halt
7F FLAG