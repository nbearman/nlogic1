import re
import argparse
import os
from pathlib import Path

name_to_byte = {
    "IMM": "00",
    "FLAG": "80",
    "EXE": "81",
    "PC": "82",
    "ALUM": "83",
    "ALUA": "84",
    "ALUB": "85",
    "ALUR": "86",
    "FPUM": "87",
    "FPUA": "88",
    "FPUB": "89",
    "FPUR": "8a",
    "RBASE": "8b",
    "ROFST": "8c",
    "RMEM": "8d",
    "WBASE": "8e",
    "WOFST": "8f",
    "WMEM": "90",
    "GPA": "91",
    "GPB": "92",
    "GPC": "93",
    "GPD": "94",
    "GPE": "95",
    "GPF": "96",
    "GPG": "97",
    "GPH": "98",
    "COMPA": "99",
    "COMPB": "9a",
    "COMPR": "9b",
    "IADN": "9c",
    "IADF": "9d",
    "LINK": "9e",
    "SKIP": "9f",
    "RTRN": "a0",
    "DMEM": "c0",
}

def address_to_bytes(addr:int) -> str:
    """
    Return string of 4 bytes separated by spaces
    "00 AA BB CC"
    """
    if addr is None:
        return "-- -- -- --"
    num = f"{addr:0>8X}"
    if len(num) > 8:
        raise Exception("address longer than 32 bits")
    return f"{num[0:2]} {num[2:4]} {num[4:6]} {num[6:8]}"

def replace_dmem(dmem:str) -> str:
    """
    Return the single-byte instruction string corresponding to
    the given DMEM macro string
    "DMEM00" -> "C0"
    """
    if not "dmem" in dmem.lower():
        raise Exception("replace_dmem called on string without dmem")
    num = int(dmem.lower().replace("dmem", ""), base=16) + 0xC0
    if num > 0xFF:
        # DMEM can only target between 00 and 3F
        # 3F + C0 == FF; do not allow instructions above FF
        raise Exception("illegal DMEM instruction")
    return f"{num:0>2X}"


class FileInfo:
    def __init__(self, filename, line_number):
        self.filename = filename
        self.line = line_number

    def get_local_label_prefix(self):
        return f"__file_{self.filename}__"

class LabelReference:
    def __init__(self, label, linked=False, file_info:FileInfo=None):
        if not linked:
            if not file_info:
                raise Exception("LabelReference created for local label with no file info")
            # change label ":label" to ":__prefix__label"
            self.label = ":" + file_info.get_local_label_prefix() + label[1:]
        else:
            self.label = label

class LabelDefinition:
    def __init__(self, label, linked=False, file_info:FileInfo=None):
        if not linked:
            # local labels need to be prefixed with file name to prevent collisions across files
            if not file_info:
                raise Exception("LabelDefinition created for local label with no file info")
            # change label "@label" to "@__prefix__label"
            self.label = "@" + file_info.get_local_label_prefix() + label[1:]
        else:
            self.label = label

        # calculate the name that will be used to look up this label
        # label references that match self.lookup refer to this label
        self.lookup = re.sub("^@", ":", re.sub("^@@", "::", self.label))

        # target VA of this label; not known until after first pass is completed
        self.va = None

class Literal:
    """
    A byte literal that appears in the source
    """
    def __init__(self, literal):
        self.literal = literal

class Fill:
    def __init__(self, fill: str):
        # convert fill instruction to int of VA it targets, 
        # "FILLFF" -> 127
        self.target = int(fill.lower().replace("fill", ""), base=16)

class Instruction:
    """
    Processor location that appears in source in traditional form
    E.g.: "WMEM", "FLAG", "GPA", "PC"
    """
    def __init__(self, inst: str):
        # original string of instruction
        self.inst = inst

        # string of byte it translates to
        self.byte = name_to_byte.get(inst)
        if not self.byte:
            # no known processor location
            raise Exception(f"cannot parse instruction {inst}")

class Line:
    """
    Object representing an original line in a source file
    """
    def parse(self, line):
        """
        return the intermediate representation of this line, a list
        of code generating items (Fill, Instruction, Literal, LabelReference, etc.)
        """
        # trim the line to only the non-commented part
        comment_start = line.find("//")
        if comment_start >= 0:
            line = line[:comment_start]

        # list of intermediate code item objects
        result = []

        for word in line.split():
            # for each word on the line, construct the matching code item
            # the order of pattern detection here determines code generation precedence

            # identify label definitions and references
            global_label_def_match = re.match("^@@.+", word)
            if global_label_def_match:
                result.append(LabelDefinition(word, linked=True))
                continue

            local_label_def_match = re.match("^@.+", word)
            if local_label_def_match:
                result.append(LabelDefinition(word, linked=False, file_info=self.file_info))
                continue

            global_label_use_match = re.match("^::.+", word)
            if global_label_use_match:
                result.append(LabelReference(word, linked=True))
                continue

            local_label_use_match = re.match("^:.+", word)
            if local_label_use_match:
                result.append(LabelReference(word, linked=False, file_info=self.file_info))
                continue
            
            # find DMEM macros
            dmem_match = re.match("(dmem)[0-9a-f][0-9a-f]$", word.lower())
            if dmem_match:
                result.append(Literal(replace_dmem(dmem_match.group())))
                continue

            # find FILL macros
            fill_match = re.match("(fill)[0-9a-f]+$", word.lower())
            if fill_match:
                result.append(Fill(word))
                continue

            # find BREAKPOINT macros
            breakpoint_match = re.match("(break)$", word.lower())
            if breakpoint_match:
                result.append(Literal("7b"))
                result.append(Literal("7b"))
                continue

            # identify byte literals
            literal_match = re.match("^[0-9a-f][0-9a-f]$", word.lower())
            if literal_match:
                result.append(Literal(word))
                continue

            # otherwise, this might be an instruction, or it might be a syntax error
            # try to parse the word as an instruction, this will raise exception if
            # unrecognizable token
            result.append(Instruction(word))
        return result

    def __init__(self, filename, line_num, line):
        self.file_info = FileInfo(filename, line_num)
        # string contents of line as it appeared in the source
        self.original_line = line
        # final VA in output assembly; not known until after first pass
        self.va = None
        # intermediate code, list of code generating item objects
        # (e.g.: Fill, Literal, LabelDefinition)
        self.intermediate = self.parse(line)

class Program:
    def __init__(self, filenames, print_final=False):
        """
        Given a list of file names, create the combined output program assembly
        and a mapping of filename (str) -> annotated debug source file lines (list(str))
        """

        # read all files and convert to line objects
        self.annotated_files = {}
        all_lines = []
        for name in filenames:
            file = open(name)
            # create entry in debug output mapping, to be populated after assembly
            self.annotated_files[name] = []

            # create a Line object with original file information
            line_num = 0
            for line in file:
                # some files start with byte order markers that need to be removed, apparently
                line = bytes(line, "utf-8").decode("utf-8-sig")
                all_lines.append(Line(name, line_num, line))
            file.close()

        # labels -> VAs to use when resolving labels after first assembly pass
        label_mapping = {}

        # final VA counter
        pc = 0

        # list of strings, output code after first pass
        # can contain literal bytes ("0A", "7F") or label references
        code = []

        # generate first pass code from all lines and populate label definition mapping
        # first pass generates all code except label references, which are left in place since
        # we don't know their final target VA until after the first pass
        # (label target VAs are calculated during first pass, in order, from line 0 to line N)
        for l in all_lines:
            # Line's intermediate holds a list of code generating items
            for item in l.intermediate:
                t = type(item)

                # record the VA of the first piece of data on this line
                # after the first piece of data, the line will have a VA, so don't overwrite it
                # ignore label definitions; they are the only instructions which do not correspond
                # to any data in the output assembly (and so have no VA)
                if t is not LabelDefinition:
                    if l.va is None:
                        l.va = pc

                # for each type of code generating item, we take different actions
                # and emit different code
                # increment PC for each emitted byte so that our VA counter is correct
                # the VA counter is used to resolve label targets as we encounter definitions
                if t is Literal:
                    code.append(item.literal)
                    pc += 1

                elif t is Instruction:
                    code.append(item.byte)
                    pc += 1
                
                elif t is Fill:
                    if pc > item.target:
                        # FILLXX places the next instruction at address XX
                        # We can't place the next instruction there if we are already past it
                        raise Exception("cannot fill; already past target")
                    while pc < item.target:
                        # push a byte and increment PC for each filled instruction until
                        # we reach the target
                        code.append("00")
                        pc += 1
                
                elif t is LabelReference:
                    # label references refer to 32 bit addresses, so increment counter by 4
                    # push the label reference as is, and we will convert it to 4 bytes in the
                    # second pass, after all label definitions are resovled
                    code.append(item.label)
                    pc += 4

                elif t is LabelDefinition:
                    # no code is generated for label definitions, so don't increment PC
                    # resolve the target of this label as the current VA
                    label_mapping[item.lookup] = pc

        # final output assembly, with all label references replaced
        # list of byte literals only, (e.g.: "7F", "A0")
        replaced_code = []

        # second pass, goal is to replace all label references with targets,
        # which are all known now that the first pass is over
        for word in code:
            # find the label in the label mapping
            label_addr = label_mapping.get(word)

            if label_addr:
                # if it exists, push the 4 bytes to the output code
                for b in address_to_bytes(label_addr).split(" "):
                    replaced_code.append(b)
            else:
                # no matching label
                if re.match("^:+", word):
                    # if it's a label reference, its target was never defined
                    raise Exception(f"untranslated label {word}")
                # otherwise, it should be a legal instruction (a regular byte literal, "7F")
                replaced_code.append(word)

        # store the final generated assembly
        self.code = replaced_code

        # populate the annotated debug mapping
        for l in all_lines:
            annotated_line = f"{address_to_bytes(l.va)} |\t{l.original_line}"
            self.annotated_files[l.file_info.filename].append(annotated_line)
            if print_final:
                print(annotated_line)

###################################################################################################
# define CLI
parser = argparse.ArgumentParser(description="Assemble given code files into program and debug source files.")
parser.add_argument(
    "code_files",
    metavar="Files",
    type=str,
    nargs="+",
    help="list of code files to assemble, in order"
)
parser.add_argument(
    "-o",
    "--out",
    metavar="Output Directory",
    type=str,
    help="directory to write output files to"
)
parser.add_argument(
    "-p",
    "--print",
    action="store_true",
    help="print final assembly to stdout"
)

# parse command line arguments
args = parser.parse_args()

# assemble program
p = Program(args.code_files, print_final=False)

# create output path if provided and it doesn't exist
if args.out:
    Path(args.out).mkdir(parents=True, exist_ok=True)

# either print the output to stdout or create program.asm in output directory
# write the assembled program to file
final_assembly = " ".join(p.code).upper()
if args.print:
    print(final_assembly)
else:
    asm_file = open(os.path.join(args.out or "", "program.asm"), "w+")
    asm_file.write(final_assembly)
    asm_file.close()

# create the annotated debug source files
for filename in p.annotated_files:
    # write the annotated files back to their original directories
    # or in subdirectory of the given output directory that matches their original directory

    # extract original directory, minus the file name
    original_dir = os.path.dirname(filename)

    # create output directory if it doesn't exist
    output_dir = os.path.join(args.out or "", original_dir)
    Path(output_dir).mkdir(parents=True, exist_ok=True)

    # calculate the final file location with file name
    location = os.path.join(output_dir, f"{os.path.split(filename)[-1]}.debug")

    # open file and write contents
    output_file = open(location, "w+")
    output_file.writelines(p.annotated_files[filename])
    output_file.close()
