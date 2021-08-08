using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace nlogic_sim
{
    public class AssemblerPro
    {
        /// <summary>
        /// Takes each file designated by the file paths and removes comments
        /// (marked with '//') and writes the results to a file of the same name
        /// with a .nlog extension.
        /// </summary>
        /// <param name="file_paths">Paths to files to parse</param>
        public static void run(string[] file_paths)
        {
            foreach (string path in file_paths)
            {
                string file_contents = File_Input.get_file_contents(path);
                file_contents = strip_comments(file_contents);
                file_contents = dmem_macro(file_contents);

                string destination = string.Format("{0}.nlog", path);
                File_Input.write_file(destination, file_contents);
            }
        }

        private static string strip_comments(string contents)
        {
            return Regex.Replace(contents, "//.*", "");
        }

        /// <summary>
        /// Replaces "DMEM##", where ## is an address (0x) based at 0, with
        /// the corresponding correct DMEM instruction
        /// </summary>
        /// <param name="contents">File contents to parse</param>
        /// <returns>The file contents with "DMEM##" macros replaced</returns>
        private static string dmem_macro(string contents)
        {
            return Regex.Replace(contents, "(dmem)[0-9a-f][0-9a-f]", new MatchEvaluator(
                match => {
                    //replace the number in the instruction with the correct instruction
                    string dmem_string = Regex.Replace(match.Value, "[0-9a-f][0-9a-f]", new MatchEvaluator(
                        num =>
                        {
                            bool parse_result = uint.TryParse(
                                num.Value,
                                System.Globalization.NumberStyles.HexNumber,
                                null,
                                out uint number);

                            //the result has to parse to a number correctly
                            Debug.Assert(parse_result);

                            //adjust the given address
                            number += Processor.DMEM;

                            //the highest DMEM instruction is FF
                            Debug.Assert(number < 0xFF, String.Format("{0} not less than 255", number));

                            Console.WriteLine("number being used: {0}", number);
                            return number.ToString("X2");
                        }
                    ), RegexOptions.IgnoreCase);

                    //remove the "DMEM" part from the input string, leaving only the instruction
                    return dmem_string.ToUpper().Replace("DMEM", "");
                }
            ), RegexOptions.IgnoreCase);
        }
    }
}