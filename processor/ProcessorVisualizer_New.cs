using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace nlogic_sim
{
    //TODO
    //make flag change color under certain conditions
    //  maybe add update_readout() event schedulers / register watchers
    //  maybe replace conditional register coloring with this logic

    public partial class Processor
    {
        /// <summary>
        /// Show the current internal state of the processor
        /// </summary>
        public void print_current_state()
        {


            update_readout(READOUT.FLAG_contents, Utility.byte_array_string(registers[FLAG].data_array, "", false));

            {
                byte[] b = Utility.byte_array_from_uint32(2, (uint)current_instruction);

                string instruction_expansion = Utility.instruction_string_from_uint32((uint)current_instruction);


                update_readout(
                    READOUT.CurrentInstruction_contents,
                    Utility.byte_array_string(b, "", false));

                update_readout(
                    READOUT.CurrentInstruction_expansion,
                    instruction_expansion);
            }


            update_readout(READOUT.EXE_contents, Utility.byte_array_string(registers[EXE].data_array, "", false));
            update_readout(READOUT.PC_contents, Utility.byte_array_string(registers[PC].data_array, "", false));

            update_readout(READOUT.SKIP_contents, Utility.byte_array_string(registers[SKIP].data_array, "", false));
            update_readout(READOUT.RTRN_contents, Utility.byte_array_string(registers[RTRN].data_array, "", false));
            update_readout(READOUT.LINK_contents, Utility.byte_array_string(registers[LINK].data_array, "", false));

            update_readout(READOUT.COMPA_contents, Utility.byte_array_string(registers[COMPA].data_array, "", false));
            update_readout(READOUT.COMPB_contents, Utility.byte_array_string(registers[COMPB].data_array, "", false));

            update_readout(READOUT.GPA_contents, Utility.byte_array_string(registers[GPA].data_array, "", false));
            update_readout(READOUT.GPB_contents, Utility.byte_array_string(registers[GPB].data_array, "", false));
            update_readout(READOUT.GPC_contents, Utility.byte_array_string(registers[GPC].data_array, "", false));
            update_readout(READOUT.GPD_contents, Utility.byte_array_string(registers[GPD].data_array, "", false));

            update_readout(READOUT.GPE_contents, Utility.byte_array_string(registers[GPE].data_array, "", false));
            update_readout(READOUT.GPF_contents, Utility.byte_array_string(registers[GPF].data_array, "", false));
            update_readout(READOUT.GPG_contents, Utility.byte_array_string(registers[GPG].data_array, "", false));
            update_readout(READOUT.GPH_contents, Utility.byte_array_string(registers[GPH].data_array, "", false));

            update_readout(READOUT.ALUM_contents, Utility.byte_array_string(registers[ALUM].data_array, "", false));
            update_readout(READOUT.ALUA_contents, Utility.byte_array_string(registers[ALUA].data_array, "", false));
            update_readout(READOUT.ALUB_contents, Utility.byte_array_string(registers[ALUB].data_array, "", false));
            update_readout(READOUT.ALUR_contents, Utility.byte_array_string(registers[ALUR].data_array, "", false));


            update_readout(READOUT.FPUM_contents, Utility.byte_array_string(registers[FPUM].data_array, "", false));
            update_readout(READOUT.FPUA_contents, Utility.byte_array_string(registers[FPUA].data_array, "", false));
            update_readout(READOUT.FPUB_contents, Utility.byte_array_string(registers[FPUB].data_array, "", false));
            update_readout(READOUT.FPUR_contents, Utility.byte_array_string(registers[FPUR].data_array, "", false));

            {
                uint alu_mode = ((Register_32)registers[ALUM]).data;
                string alu_mode_string = alu_mode.ToString("X2");
                if (Utility.alu_mode_to_name.ContainsKey((ALU_MODE)alu_mode))
                {
                    alu_mode_string = Utility.alu_mode_to_name[(ALU_MODE)alu_mode];
                }

                //alu_mode_string = String.Format("{0, -5}", alu_mode_string);
                update_readout(READOUT.ALUM_expansion, alu_mode_string);
            }

            {
                uint fpu_mode = ((Register_32)registers[FPUM]).data;
                string fpu_mode_string = fpu_mode.ToString("X2");
                if (Utility.fpu_mode_to_name.ContainsKey((FPU_MODE)fpu_mode))
                {
                    fpu_mode_string = Utility.fpu_mode_to_name[(FPU_MODE)fpu_mode];
                }

                //fpu_mode_string = String.Format("{0, -5}", fpu_mode_string);
                update_readout(READOUT.FPUM_expansion, fpu_mode_string);
            }

            {
                string int_value = ((Register_32)registers[ALUA]).data.ToString();
                update_readout(READOUT.ALUA_expansion, int_value);
            }
            {
                string int_value = ((Register_32)registers[ALUB]).data.ToString();
                update_readout(READOUT.ALUB_expansion, int_value);
            }
            {
                string int_value = ((Register_32)registers[ALUR]).data.ToString();
                update_readout(READOUT.ALUR_expansion, int_value);
            }
            {
                string float_value = ((Register_32)registers[FPUA]).float_data().ToString();
                update_readout(READOUT.FPUA_expansion, float_value);
            }
            {
                string float_value = ((Register_32)registers[FPUB]).float_data().ToString();
                update_readout(READOUT.FPUB_expansion, float_value);
            }
            {
                string float_value = ((Register_32)registers[FPUR]).float_data().ToString();
                update_readout(READOUT.FPUR_expansion, float_value);
            }

            update_readout(READOUT.RBASE_contents, Utility.byte_array_string(registers[RBASE].data_array, "", false));
            update_readout(READOUT.ROFST_contents, Utility.byte_array_string(registers[ROFST].data_array, "", false));

            update_readout(READOUT.WBASE_contents, Utility.byte_array_string(registers[WBASE].data_array, "", false));
            update_readout(READOUT.WOFST_contents, Utility.byte_array_string(registers[WOFST].data_array, "", false));

            uint iadn_address = ((Register_32)registers[PC]).data + 4;
            uint iadf_address = ((Register_32)registers[PC]).data + 6;

            bool comparison = ((Register_32)registers[COMPA]).data == ((Register_32)registers[COMPB]).data;
            uint compr_address = ((Register_32)registers[PC]).data + (comparison ? (uint)0 : (uint)4);
            update_readout(READOUT.COMPR_contents, get_memory_value_for_visualizer(compr_address));
            update_readout(READOUT.IADN_contents, get_memory_value_for_visualizer(iadn_address));
            update_readout(READOUT.IADF_contents, get_memory_value_for_visualizer(iadf_address));

            uint rmem_address = ((Register_32)registers[RBASE]).data + ((Register_32)registers[ROFST]).data;
            uint wmem_address = ((Register_32)registers[WBASE]).data + ((Register_32)registers[WOFST]).data;
            update_readout(READOUT.RMEM_contents, get_memory_value_for_visualizer(rmem_address));
            update_readout(READOUT.WMEM_contents, get_memory_value_for_visualizer(wmem_address));


            update_memory_context(READOUT.MemoryContext1, get_neighboring_memory_lines(rmem_address));
            update_memory_context(READOUT.MemoryContext2, get_neighboring_memory_lines(wmem_address));


        }

        public void initialize_visualizer()
        {
            //initialize the cache
            Array values = Enum.GetValues(typeof(READOUT));
            foreach (var v in values)
            {
                readout_cache.Add((READOUT)v, new ColorString[0]);
            }

            //print skeleton
            print_skeleton();

            //populate name fields
            print_all_names();


        }

        //the current state of each readout
        private static Dictionary<READOUT, ColorString[]> readout_cache = new Dictionary<READOUT, ColorString[]>();

        /// <summary>
        /// Write the given ColorStrings to the given READOUT
        /// </summary>
        /// <param name="location">The READOUT to update</param>
        /// <param name="formatted_value">Fromatted value to display</param>
        private static void update_readout(READOUT location, ColorString[] formatted_value)
        {
            //the memory context readouts cannot be updated with this method
            if (location == READOUT.MemoryContext1 || location == READOUT.MemoryContext2)
            {
                throw new Exception("cannot use update_readout() for memory contexts; " +
                    "use update_memory_context() instead");
            }

            //check the cache to avoid rewriting unchanged data
            if (readout_cache[location].SequenceEqual(formatted_value))
            {
                //no change in value already displayed
                return;
            }

            else
            {
                //the value shown needs to be changed

                //save the previous color and cursor position
                ConsoleColor previous_color = Console.ForegroundColor;
                Tuple<int, int> previous_coordinates =
                    new Tuple<int, int>(Console.CursorLeft, Console.CursorTop);

                //clear the current location (in case the new value doesn't overwrite all spaces)
                clear_readout(location);

                //print the formatted string at the correct location
                Tuple<int, int, int> coordinates = readout_coordinates[location];
                Console.SetCursorPosition(coordinates.Item1, coordinates.Item2);
                foreach (var colorstring in formatted_value)
                    colorstring.print();

                //update the cache
                readout_cache[location] = formatted_value;

                //reset the console color and cursor position
                Console.ForegroundColor = previous_color;
                Console.SetCursorPosition(previous_coordinates.Item1, previous_coordinates.Item2);
            }
        }

        /// <summary>
        /// Change the specified readout to display the given value using the location's default styling
        /// </summary>
        /// <param name="location">READOUT to be changed</param>
        /// <param name="value">Unformatted value to display</param>
        private static void update_readout(READOUT location, string value)
        {

            //format the given string
            ColorString[] formatted_value = format_to_readout_style(location, value);

            //update the given readout
            update_readout(location, formatted_value);   


        }

        private static void update_memory_context(READOUT memory_context, ColorString[][] neighboring_lines)
        {
            if (!(memory_context == READOUT.MemoryContext1 || memory_context == READOUT.MemoryContext2))
            {
                throw new Exception("the supplied READOUT must be a memory context readout");
            }

            //save the previous color and cursor position
            ConsoleColor previous_color = Console.ForegroundColor;
            Tuple<int, int> previous_coordinates =
                new Tuple<int, int>(Console.CursorLeft, Console.CursorTop);

            Tuple<int, int, int> start_position = readout_coordinates[memory_context];
            //print all the lines of the context
            for (int i = 0; i < neighboring_lines.Length; i++)
            {
                Console.SetCursorPosition(start_position.Item1, start_position.Item2 + i);
                Console.Write("  ");
                foreach (var colorstring in neighboring_lines[i])
                {
                    colorstring.print();
                    Console.Write(' ');
                }
            }

            //reset the console color and cursor position
            Console.ForegroundColor = previous_color;
            Console.SetCursorPosition(previous_coordinates.Item1, previous_coordinates.Item2);
        }

        /// <summary>
        /// Print the string at the specified memory_context, using memory context formatting.
        /// Supplying a READOUT other than a memory context will cause an error.
        /// </summary>
        /// <param name="memory_context">READOUT specifying which context to print to</param>
        private static void update_memory_context(READOUT memory_context, uint address, byte[] memory)
        {
            if (!(memory_context == READOUT.MemoryContext1 || memory_context == READOUT.MemoryContext2))
            {
                throw new Exception("the supplied READOUT must be a memory context readout");
            }

            //get the neighboring lines of memory as ColorStrings
            ColorString[][] neighboring_lines = get_neighboring_memory_lines(address, memory);

            //save the previous color and cursor position
            ConsoleColor previous_color = Console.ForegroundColor;
            Tuple<int, int> previous_coordinates = 
                new Tuple<int, int>(Console.CursorLeft, Console.CursorTop);

            Tuple<int, int, int> start_position = readout_coordinates[memory_context];
            //print all the lines of the context
            for (int i = 0; i < neighboring_lines.Length; i++)
            {
                Console.SetCursorPosition(start_position.Item1, start_position.Item2 + i);
                Console.Write("  ");
                foreach (var colorstring in neighboring_lines[i])
                {
                    colorstring.print();
                    Console.Write(' ');
                }
            }

            //reset the console color and cursor position
            Console.ForegroundColor = previous_color;
            Console.SetCursorPosition(previous_coordinates.Item1, previous_coordinates.Item2);

        }

        private string get_memory_value_for_visualizer(uint address)
        {
            byte[] data = this.environment.read_address(address, 4, true);
            if (data == null)
                return "????????";
            return Utility.uint32_from_byte_array(data).ToString("X8");
        }

        private ColorString[][] get_neighboring_memory_lines(uint address)
        {
            //8 bytes per line, so line number is byte's address divided by 8
            uint line_number = address / 8;

            //aim to show 3 lines before the current line, but not before the first line
            uint base_line_number = line_number < 3 ? 0 : line_number - 3;

            //last possible line in virtual address space
            uint last_line = (uint.MaxValue / 8) - 1;

            //don't put the base closer than the 8th line from the end
            if (base_line_number > last_line - 8)
                base_line_number = last_line - 7;

            //readout shows 64 total bytes
            List<string> byte_strings = new List<string>();

            uint starting_address = base_line_number * 8;

            //read memory 4 bytes at a time
            //since the readout is page-aligned, it should be the case
            //that, for each read, either all 4 bytes are readable or
            //none of the 4 are
            for (uint i = 0; i < 16; i++)
            {
                uint next_address = starting_address + (4 * i);
                byte[] next_4_bytes = this.environment.read_address(next_address, 4, true);

                //add all the bytes as strings to the list of byte strings
                //if the read failed, add "??" for each byte
                for (int b = 0; b < 4; b++)
                {
                    if (next_4_bytes == null)
                        byte_strings.Add("??");
                    else
                        byte_strings.Add(next_4_bytes[b].ToString("X2"));
                }
            }

            Debug.Assert(byte_strings.Count == 64, "the incorrect number of bytes was retrieved during context update");

            ColorString[] all_color_strings = new ColorString[64];
            for (int i = 0; i < byte_strings.Count; i++)
            {
                ColorString m = new ColorString();
                m.value = byte_strings[i];
                if (starting_address + i >= address && starting_address + i < address + 4)
                    m.color = ConsoleColor.White;
                else
                    m.color = ConsoleColor.DarkGray;
                all_color_strings[i] = m;
            }

            ColorString[][] result = new ColorString[8][];

            for (int row = 0; row < 8; row++)
            {
                result[row] = new ColorString[8];

                for (int col = 0; col < 8; col++)
                {
                    result[row][col] = all_color_strings[(row * 8) + col];
                }
            }

            return result;
        }

        /// <summary>
        /// Returns an array of ColorString arrays representing the region of memory
        /// surrounding the given address.
        /// Each ColorString[] in the returned array is a formatted row of the memory context readout
        /// </summary>
        /// <param name="address">The selected address around which to read memory</param>
        /// <param name="memory">The memory to read from</param>
        /// <returns>An array of ColorString[], each the formatted representation of one row of the
        /// memory context readout.</returns>
        private static ColorString[][] get_neighboring_memory_lines(uint address, byte[] memory)
        {
            ColorString[][] result = new ColorString[8][];

            int line_number = (int)(address / 8);
            int base_line_number = line_number - 3;

            if (base_line_number < 0)
            {
                base_line_number = 0;
            }

            int last_line = ((memory.Length) / 8) - 1;

            if (base_line_number > last_line - 8)
            {
                base_line_number = last_line - 7;
            }

            for (int row = 0; row < 8; row++)
            {
                result[row] = new ColorString[8];
                int addr = ((int)base_line_number + row) * 8;

                for (int col = 0; col < 8; col++)
                {
                    ColorString m = new ColorString();
                    m.value = memory[addr + col].ToString("X2");
                    if ((addr + col) >= address && (addr + col) < (address + 4))
                        m.color = ConsoleColor.White;
                    else
                        m.color = ConsoleColor.DarkGray;
                    result[row][col] = m;
                }
            }

            return result;
        }

        /// <summary>
        /// Returns a string in the format expected by the given readout
        /// </summary>
        /// <param name="location">READOUT which style will be used</param>
        /// <param name="value">String to format</param>
        /// <returns>Formatted ColorString using location's style</returns>
        private static ColorString[] format_to_readout_style(READOUT location, string value)
        {

            List<ColorString> result_list = new List<ColorString>();


            switch (location)
            {
                case (READOUT.ALUA_contents):
                case (READOUT.ALUB_contents):
                case (READOUT.ALUM_contents):
                case (READOUT.ALUR_contents):
                case (READOUT.COMPA_contents):
                case (READOUT.COMPB_contents):
                case (READOUT.COMPR_contents):
                case (READOUT.EXE_contents):
                case (READOUT.FLAG_contents):
                case (READOUT.FPUA_contents):
                case (READOUT.FPUB_contents):
                case (READOUT.FPUM_contents):
                case (READOUT.FPUR_contents):
                case (READOUT.GPA_contents):
                case (READOUT.GPB_contents):
                case (READOUT.GPC_contents):
                case (READOUT.GPD_contents):
                case (READOUT.GPE_contents):
                case (READOUT.GPF_contents):
                case (READOUT.GPG_contents):
                case (READOUT.GPH_contents):
                case (READOUT.IADF_contents):
                case (READOUT.IADN_contents):
                case (READOUT.LINK_contents):
                case (READOUT.PC_contents):
                case (READOUT.RBASE_contents):
                case (READOUT.RMEM_contents):
                case (READOUT.ROFST_contents):
                case (READOUT.RTRN_contents):
                case (READOUT.SKIP_contents):
                case (READOUT.WBASE_contents):
                case (READOUT.WMEM_contents):
                case (READOUT.WOFST_contents):
                    {
                        //standard register contents
                        //take a maximum of 4 doublets
                        //pad string with 2 spaces on both sides
                        ColorString cs = new ColorString();
                        cs.value = format_register(value, 4, 2, "  ", "  ");

                        //white when not "  00 00 00 00  "
                        if (cs.value != "  00 00 00 00  ")
                            cs.color = ConsoleColor.White;
                        //dark gray otherwise
                        else
                            cs.color = ConsoleColor.DarkGray;
                        result_list.Add(cs);
                        break;
                    }
                case (READOUT.CurrentInstruction_contents):
                    {
                        //TODO color instructions conditionally

                        //same as standard register contents
                        //take only 2 doublets
                        ColorString cs = new ColorString();
                        cs.value = format_register(value, 2, 2, "  ", "  ");

                        //white when not "  00 00  "
                        if (cs.value != "  00 00  ")
                            cs.color = ConsoleColor.White;
                        //dark gray otherwise
                        else
                            cs.color = ConsoleColor.DarkGray;
                        result_list.Add(cs);
                        break;
                    }
                case (READOUT.CurrentInstruction_expansion):
                    {
                        Debug.Assert(value.Contains("->"), "Current instruction expansion must be passed containing '->'");

                        //TODO make the arrow appear

                        //pad string with 1 space to the left
                        string[] separated = value.Split(new string[] { "->" }, StringSplitOptions.None);

                        //format each half to be properly spaced
                        separated[0] = String.Format("{0, -5}", separated[0]);
                        separated[1] = String.Format("{0, -5}", separated[1]);
                        
                        //create a ColorString for each section of the string
                        foreach (string s in separated)
                        {
                            ColorString cs = new ColorString();
                            cs.value = s;
                            string register_name = s.Trim();
                            //color known register names with the corresponding color
                            if (Processor.register_name_to_color.Keys.Contains(register_name))
                                cs.color = Processor.register_name_to_color[register_name];
                            else
                                cs.color = ConsoleColor.Gray;

                            result_list.Add(cs);
                        }

                        result_list.Insert(1, new ColorString("->", ConsoleColor.Gray));
                        break;
                    }
                case (READOUT.ALUM_expansion):
                case (READOUT.FPUM_expansion):
                    {
                        //pad string with 1 space on both sides
                        ColorString cs = new ColorString(" " + value.Substring(0, Math.Min(value.Length, 4)) + " ", ConsoleColor.Gray);
                        result_list.Add(cs);
                        break;
                    }
                case (READOUT.ALUA_expansion):
                case (READOUT.ALUB_expansion):
                case (READOUT.ALUR_expansion):
                    {
                        //pad string with 1 space on both sides
                        string int_value = String.Format("{0, 10}", value);
                        ColorString cs = new ColorString(" " + int_value + " ", ConsoleColor.Gray);
                        result_list.Add(cs);
                        break;
                    }
                case (READOUT.FPUA_expansion):
                case (READOUT.FPUB_expansion):
                case (READOUT.FPUR_expansion):
                    {
                        //pad string with 1 space on both sides
                        //format float
                        string float_value = String.Format("{0, 10}", value);
                        ColorString cs = new ColorString(" " + float_value + " ", ConsoleColor.Gray);
                        result_list.Add(cs);
                        break;
                    }
                default:
                    //do nothing
                    result_list.Add(new ColorString(value, ConsoleColor.Gray));
                    break;
            }

            return result_list.ToArray();
        }

        /// <summary>
        /// Changes the given string to be of the form "[padR]AB CD EF GH[padL]"
        /// </summary>
        /// <param name="value">String to be formatted</param>
        /// <param name="num_groups">Number of groups of characters allowed</param>
        /// <param name="group_size">Size of character groups in characters</param>
        /// <param name="padL">Padding for the left side</param>
        /// <param name="padR">Padding for the right side</param>
        /// <returns></returns>
        private static string format_register(string value, int num_groups, int group_size, string padL, string padR)
        {
            //standard register contents
            //take a maximum of 4 doublets
            //pad string with 2 spaces on both sides
            string truncated_string = value.Substring(0, Math.Min(value.Length, group_size * num_groups));
            string result = padL;
            for (int i = 0; i < truncated_string.Length; i++)
            {
                //split string into doublets separated by spaces
                if (i % group_size == 0 && i > 0)
                    result += " ";
                result += truncated_string[i];
            }
            result += padR;

            return result;
        }

        /// <summary>
        /// Write spaces to the entire writeable area of a READOUT
        /// </summary>
        /// <param name="location">READOUT to clear</param>
        private static void clear_readout(READOUT location)
        {
            //save the previous color and cursor position
            ConsoleColor previous_color = Console.ForegroundColor;
            Tuple<int, int> previous_coordinates =
                new Tuple<int, int>(Console.CursorLeft, Console.CursorTop);

            //write spaces to the readout
            Tuple<int, int, int> readout_info = readout_coordinates[location];
            Console.CursorLeft = readout_info.Item1;
            Console.CursorTop = readout_info.Item2;
            for (int i = 0; i < readout_info.Item3; i++)
            {
                Console.Write(" ");
            }

            //clear the cache
            readout_cache[location] = new ColorString[0];

            //reset the console color and cursor position
            Console.ForegroundColor = previous_color;
            Console.SetCursorPosition(previous_coordinates.Item1, previous_coordinates.Item2);
        }

        /// <summary>
        /// Fill all READOUTs entirely with spaces.
        /// </summary>
        private static void clear_all_readouts()
        {
            Array values = Enum.GetValues(typeof(READOUT));
            foreach (var v in values)
            {
                clear_readout((READOUT)v);
            }
        }

        /// <summary>
        /// String with color information
        /// </summary>
        private struct ColorString
        {
            public string value;
            public ConsoleColor color;

            public ColorString(string value, ConsoleColor color)
            {
                this.value = value;
                this.color = color;
            }

            public void print()
            {
                Console.ForegroundColor = this.color;
                Console.Write(value);
            }


        }

        /// <summary>
        /// Print all the names of registers and readouts.
        /// </summary>
        private static void print_all_names()
        {
            Array values = Enum.GetValues(typeof(READOUT));
            foreach (var v in values)
            {
                READOUT target = (READOUT)v;

                if (register_name_to_color.ContainsKey(target.ToString()))
                {
                    update_readout(
                        target,
                        new ColorString[]
                        {
                            new ColorString
                            (
                                (" " + target.ToString()),
                                register_name_to_color[target.ToString()]
                            )
                        });
                }
            }

        }

        /// <summary>
        /// Print the skeleton of the display to the console
        /// </summary>
        private static void print_skeleton()
        {
            Console.WriteLine("|");
            Console.WriteLine("============================================================================================================");
            Console.WriteLine("##||       |               ||##||                     |         |                 ||##");
            Console.WriteLine("######################################################################################");
            Console.WriteLine();
            Console.WriteLine("  ||       |               ||    ||       |               ||    ||       |               || ]>      <[");
            Console.WriteLine("  ||       |               ||    ||       |               ||    ||       |               || [            ]");
            Console.WriteLine("                           ||    ||       |               ||    ||       |               || [            ]");
            Console.WriteLine("  ||       |               ||    ||       |               ||    ||       |               || [            ]");
            Console.WriteLine("  ||       |               ||");
            Console.WriteLine("  ||       |               ||    ||       |               ||    ||       |               || ]>      <[");
            Console.WriteLine("                           ||    ||       |               ||    ||       |               || [            ]");
            Console.WriteLine("                           ||    ||       |               ||    ||       |               || [            ]");
            Console.WriteLine("  ||       |               ||    ||       |               ||    ||       |               || [            ]");
            Console.WriteLine("  ||       |               ||");
            Console.WriteLine("  ||       |               ||");
            Console.WriteLine();
            Console.WriteLine("  ||       |               ||");
            Console.WriteLine("  ||       |               ||");
            Console.WriteLine();
            Console.WriteLine("_____________________________________________________________________________________________________________");
            Console.WriteLine("  ||       |               ||                           ||       |               ||");
            Console.WriteLine("  ||       |               ||                           ||       |               ||");
            Console.WriteLine("  ||       |               ||                           ||       |               ||");
        }
    }
}