using System;
using System.Runtime;
using System.Collections.Generic;

namespace nlogic_sim
{
    class Assembler
    {
        private static bool output_to_file = false;

        private static string assembler_output = "";

        public static bool assembled = false;

        public static byte[] program_data;

        public static string disassembly;

        public static string dump_assembly()
        {
            if (program_data != null)
                return Utility.byte_array_string(program_data);
            return "";
        }

        public static string generate_disassembly()
        {
            if (program_data == null)
            {
                return "";
            }

            string result = "";
            for (int i = 0; i < Assembler.program_data.Length; i += 2)
            {
                byte[] instruction = new byte[2];
                Array.Copy(Assembler.program_data, i, instruction, 0, 2);
                string expansion = Utility.instruction_string_from_byte_array(instruction);
                result += expansion + "\n";
            }

            return result;
        }

        //attach a prefix to all local labels
        private static bool prefix_labels(string code, string label_prefix, out string result)
        {
            result = "";

            string[] split = code.Split(new string[] { " ", "\t", "\n", "\r", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < split.Length; i++)
            {
                //for local label usages and definitions, but not linked labels
                if ( (split[i].Substring(0, 1)[0] == '@' && (split[i].Substring(0, 2) != "@@"))
                    || (split[i].Substring(0, 1)[0] == ':' && (split[i].Substring(0, 2) != "::")) )
                {
                    split[i] = split[i].Substring(0, 1)[0] + label_prefix + split[i].Substring(1);
                }

                result += split[i];
                if (i < split.Length - 1)
                {
                    result += ' ';
                }
            }

            return true;
        }

        //resolve, remove, and replace all label usages and definitions
        private static bool strip_labels(string code, out string result)
        {
            result = "";

            //dictionary of defined labels, along with their address of definition
            Dictionary<string, uint> labels = new Dictionary<string, uint>();

            uint address_counter = 0;

            string[] split = code.Split(new string[] { " ", "\t", "\n", "\r", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);


            //go through input once to find all label definitions
            for (int i = 0; i < split.Length; i++)
            {
                if (split[i].Substring(0, 1)[0] == '@')
                {
                    string l = split[i].Substring(1);
                    if (labels.ContainsKey(l))
                    {
                        //duplicate label defined
                        print_message("duplicate label defined: " + l, MESSAGE_TYPE.Error);
                        return false;
                    }

                    labels.Add(l, address_counter);
                }

                else
                {
                    //reserve 4 addresses for labels that will be replaced with 4 bytes
                    if (split[i].Substring(0, 1)[0] == ':')
                        address_counter += 4;
                    else
                        address_counter += 1;
                }

            }

            //go through input again, removing label definitions and replacing label uses with addresses
            //detect undefined label usage
            for (int i = 0; i < split.Length; i++)
            {
                if (split[i].Substring(0, 1)[0] == '@')
                {
                    split[i] = "";
                }

                else if (split[i].Substring(0, 1)[0] == ':')
                {
                    string l = split[i].Substring(1);

                    //linked labels must strip the additional ':' prefix
                    //replace the ':' with @ because linked labels are stored with the '@' prefix
                    if (l[0] == ':')
                        l = '@' + l.Substring(1);

                    if (labels.ContainsKey(l))
                    {
                        byte[] address_bytes = Utility.byte_array_from_uint32(4, labels[l]);
                        split[i] = Utility.byte_array_string(address_bytes);
                    }

                    else
                    {
                        //undefined label used
                        print_message("undefined label: " + l, MESSAGE_TYPE.Error);
                        return false;
                    }
                }

                result += split[i];
                if (i < split.Length - 1)
                {
                    result += ' ';
                }
            }

            return true;
        }

        private static bool replace_fills(string amalgamated_code, out string instructions)
        {
            instructions = "";
            string[] split = amalgamated_code.Split(new string[] { " ", "\t", "\n", "\r", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            int i = 0;
            for (int inst = 0; inst < split.Length; inst++)
            {
                string next = split[inst];
                if (next.Contains(":"))
                {
                    //count references to labels that will be replaced with addresses as 4 bytes
                    i += 4;
                    instructions += next + " ";
                }
                else if (next.Contains("@"))
                {
                    //don't count definitions of labels; they don't impact placement of instructions
                    i += 0;
                    instructions += next + " ";
                }
                else if (next.Contains("FILL"))
                {
                    uint fill_target;
                    bool parsed_fill = uint.TryParse(
                                next.Replace("FILL", ""),
                                System.Globalization.NumberStyles.HexNumber,
                                null,
                                out fill_target);

                    if (!parsed_fill)
                    {
                        Assembler.print_message("FILL not followed by number: \t\t\"" + next + "\"", MESSAGE_TYPE.Error);
                        return false;
                    }

                    if (fill_target < i)
                    {
                        Assembler.print_message("Already past fill target, cannot fill: \t\t\"" + fill_target + "\"", MESSAGE_TYPE.Error);
                        return false;
                    }

                    while (i < fill_target)
                    {
                        instructions += "00 ";
                        i += 1;
                    }
                }
                else
                {
                    i += 1;
                    instructions += next + " ";
                }
            }
            return true;
        }


        private static bool code_to_instructions(string[] code_files, out string instructions)
        {
            instructions = "";

            string amalgamated_code = "";

            for (int i = 0; i < code_files.Length; i++)
            {
                string prefix = "__file_" + i + "__";
                string result;
                Assembler.prefix_labels(code_files[i], prefix, out result);
                amalgamated_code += result;
                
                //add a space between two code files
                if (i < code_files.Length - 1)
                {
                    amalgamated_code += " ";
                }
            }

            //string[] split = amalgamated_code.Split(new string[] { " ", "\t", "\n", "\r", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            //for (int i = 0; i < split.Length; i++)
            //{
            //    Console.WriteLine(split[i]);
            //}

            //return the success state of the assembly process so far
            //also pass out intructions

            if (!replace_fills(amalgamated_code, out instructions))
                return false;
            return strip_labels(instructions, out instructions);
        }

        private static bool instructions_to_binary(string instructions)
        {
            bool successful = true;

            string input_string = instructions;
            

            string[] split = input_string.Split(new string[] { " ", "\t", "\n", "\r", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            byte[] converted_data = new byte[split.Length];

            string next = "";
            for (int i = 0; i < split.Length; i++)
            {
                next = split[i];

                if (Utility.register_name_to_location.ContainsKey(next))
                {
                    converted_data[i] = Utility.register_name_to_location[next];
                }

                else
                {
                    byte number;
                    //bool result = byte.TryParse(next, out number);
                    bool result = byte.TryParse(next, System.Globalization.NumberStyles.HexNumber, null, out number);

                    if (result)
                    {
                        converted_data[i] = number;

                        if (number >= 0x80)
                        {
                            Assembler.print_message("literal number not < 0x80: \t\ti = " + i, MESSAGE_TYPE.Warning);
                        }
                    }

                    else
                    {
                        Assembler.print_message("unrecognized input \"" + next + "\" : \t\ti = " + i, MESSAGE_TYPE.Error);
                        successful = false;
                    }
                }
            }

            program_data = converted_data;

            return successful;
        }

        public static void assemble(string[] filepaths, string output_file_path = null)
        {

            if (output_file_path != null)
            {
                redirect_output(output_file_path);
            }

            ConsoleColor original_color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("|ASSEMBLER");
            Console.ForegroundColor = ConsoleColor.Gray;

            Assembler.print_message("opening file for assembly...", MESSAGE_TYPE.Status);

            string[] code_files = new string[filepaths.Length];

            bool successful = true;

            for (int i = 0; i < filepaths.Length; i++)
            {

                try
                {
                    code_files[i] = File_Input.get_file_contents(filepaths[i]);
                }
                catch (Exception e)
                {
                    Assembler.print_message("unable to open file: \t\t" + filepaths[i], MESSAGE_TYPE.Error);
                    successful = false;
                }
            }


            bool assembler_result = false;

            if (successful)
            {
                string instructions = "";
                assembler_result = code_to_instructions(code_files, out instructions);
                if (assembler_result)
                    assembler_result = instructions_to_binary(instructions);
            }

            if (assembler_result)
            {
                Assembler.disassembly = Assembler.generate_disassembly();
                Assembler.print_message("assembly successful", MESSAGE_TYPE.Success);
                assembled = true;
            }
            else
            {
                Assembler.print_message("assembly failure; output unreliable", MESSAGE_TYPE.Failure);
            }



            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("|end ASSEMBLER");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.ForegroundColor = original_color;

            if (output_file_path != null)
            {
                reset_output();
            }

        }

        private enum MESSAGE_TYPE
        {
            None = (int)ConsoleColor.Gray,
            Warning = (int)ConsoleColor.Yellow,
            Error = (int)ConsoleColor.Red,
            Failure = (int)ConsoleColor.DarkRed,
            Status = (int)ConsoleColor.Blue,
            Success = (int)ConsoleColor.Green
        }

        private static void print_message(string message, MESSAGE_TYPE message_type = MESSAGE_TYPE.None)
        {

            ConsoleColor original_color = Console.ForegroundColor;
            Console.ForegroundColor = (ConsoleColor)message_type;

            Console.Write("|\t:> ");
            switch (message_type)
            {
                case (MESSAGE_TYPE.Warning):
                    Console.ForegroundColor = (ConsoleColor)MESSAGE_TYPE.Warning;
                    Console.Write("Warning: ");
                    break;
                case (MESSAGE_TYPE.Error):
                    Console.ForegroundColor = (ConsoleColor)MESSAGE_TYPE.Error;
                    Console.Write("Error: ");
                    break;
                case (MESSAGE_TYPE.Failure):
                    Console.ForegroundColor = (ConsoleColor)MESSAGE_TYPE.Failure;
                    Console.Write("Failure: ");
                    break;
                case (MESSAGE_TYPE.Status):
                    Console.ForegroundColor = (ConsoleColor)MESSAGE_TYPE.Status;
                    Console.Write("Status: ");
                    break;
                case (MESSAGE_TYPE.Success):
                    Console.ForegroundColor = (ConsoleColor)MESSAGE_TYPE.Success;
                    Console.Write("Success: ");
                    break;
                default:
                    break;
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(message);

            Console.ForegroundColor = original_color;
        }


        private static System.IO.TextWriter original_out_stream;
        private static System.IO.StreamWriter file_out_stream;

        private static void redirect_output(string file_path)
        {
            original_out_stream = Console.Out;
            file_out_stream = new System.IO.StreamWriter(file_path, false);
            Console.SetOut(file_out_stream);
        }

        private static void reset_output()
        {
            Console.SetOut(original_out_stream);
            file_out_stream.Close();
        }
    }
}