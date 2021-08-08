using System;
using System.Collections.Generic;

namespace nlogic_sim
{
    public partial class Processor
    {
        private enum READOUT
        {
            Header = 0,
            FLAG = 1, FLAG_contents = 2,
            CurrentInstruction, CurrentInstruction_contents, CurrentInstruction_expansion,
            EXE, EXE_contents,
            PC, PC_contents,
            SKIP, SKIP_contents,
            RTRN, RTRN_contents,
            LINK, LINK_contents,
            COMPA, COMPA_contents,
            COMPB, COMPB_contents,
            COMPR, COMPR_contents,
            IADN, IADN_contents,
            IADF, IADF_contents,
            GPA, GPA_contents,
            GPB, GPB_contents,
            GPC, GPC_contents,
            GPD, GPD_contents,
            GPE, GPE_contents,
            GPF, GPF_contents,
            GPG, GPG_contents,
            GPH, GPH_contents,
            ALUM, ALUM_contents, ALUM_expansion,
            ALUA, ALUA_contents, ALUA_expansion,
            ALUB, ALUB_contents, ALUB_expansion,
            ALUR, ALUR_contents, ALUR_expansion,
            FPUM, FPUM_contents, FPUM_expansion,
            FPUA, FPUA_contents, FPUA_expansion,
            FPUB, FPUB_contents, FPUB_expansion,
            FPUR, FPUR_contents, FPUR_expansion,
            RBASE, RBASE_contents,
            ROFST, ROFST_contents,
            RMEM, RMEM_contents,
            WBASE, WBASE_contents,
            WOFST, WOFST_contents,
            WMEM, WMEM_contents,
            MemoryContext1,
            MemoryContext2,
        };

        /// <summary>
        /// Mapping of READOUT locations to (left, top, width) tuples
        /// Left is the x-coordinate of the READOUT, top is the y-coordinate
        /// Width is the number of chars allotted to this location
        /// </summary>
        private static Dictionary<READOUT, Tuple<int, int, int>> readout_coordinates = new Dictionary<READOUT, Tuple<int, int, int>>
        {
            { READOUT.Header, new Tuple<int, int, int>(1, 0, 40) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.FLAG, new Tuple<int, int, int>(4, 2, 7) },
            { READOUT.FLAG_contents, new Tuple<int, int, int>(12, 2, 15) },
            { READOUT.CurrentInstruction, new Tuple<int, int, int>(33, 2, 21) },
            { READOUT.CurrentInstruction_contents, new Tuple<int, int, int>(55, 2, 9) },
            { READOUT.CurrentInstruction_expansion, new Tuple<int, int, int>(65, 2, 17) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.EXE, new Tuple<int, int, int>(4, 5, 7) },
            { READOUT.EXE_contents, new Tuple<int, int, int>(12, 5, 15) },
            { READOUT.GPA, new Tuple<int, int, int>(35, 5, 7) },
            { READOUT.GPA_contents, new Tuple<int, int, int>(43, 5, 15) },
            { READOUT.ALUM, new Tuple<int, int, int>(66, 5, 7) },
            { READOUT.ALUM_contents, new Tuple<int, int, int>(74, 5, 15) },
            { READOUT.ALUM_expansion, new Tuple<int, int, int>(94, 5, 6) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.PC, new Tuple<int, int, int>(4, 6, 7) },
            { READOUT.PC_contents, new Tuple<int, int, int>(12, 6, 15) },
            { READOUT.GPB, new Tuple<int, int, int>(35, 6, 7) },
            { READOUT.GPB_contents, new Tuple<int, int, int>(43, 6, 15) },
            { READOUT.ALUA, new Tuple<int, int, int>(66, 6, 7) },
            { READOUT.ALUA_contents, new Tuple<int, int, int>(74, 6, 15) },
            { READOUT.ALUA_expansion, new Tuple<int, int, int>(93, 6, 11) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.GPC, new Tuple<int, int, int>(35, 7, 7) },
            { READOUT.GPC_contents, new Tuple<int, int, int>(43, 7, 15) },
            { READOUT.ALUB, new Tuple<int, int, int>(66, 7, 7) },
            { READOUT.ALUB_contents, new Tuple<int, int, int>(74, 7, 15) },
            { READOUT.ALUB_expansion, new Tuple<int, int, int>(93, 7, 11) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.SKIP, new Tuple<int, int, int>(4, 8, 7) },
            { READOUT.SKIP_contents, new Tuple<int, int, int>(12, 8, 15) },
            { READOUT.GPD, new Tuple<int, int, int>(35, 8, 7) },
            { READOUT.GPD_contents, new Tuple<int, int, int>(43, 8, 15) },
            { READOUT.ALUR, new Tuple<int, int, int>(66, 8, 7) },
            { READOUT.ALUR_contents, new Tuple<int, int, int>(74, 8, 15) },
            { READOUT.ALUR_expansion, new Tuple<int, int, int>(93, 8, 11) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.RTRN, new Tuple<int, int, int>(4, 9, 7) },
            { READOUT.RTRN_contents, new Tuple<int, int, int>(12, 9, 15) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.LINK, new Tuple<int, int, int>(4, 10, 7) },
            { READOUT.LINK_contents, new Tuple<int, int, int>(12, 10, 15) },
            { READOUT.GPE, new Tuple<int, int, int>(35, 10, 7) },
            { READOUT.GPE_contents, new Tuple<int, int, int>(43, 10, 15) },
            { READOUT.FPUM, new Tuple<int, int, int>(66, 10, 7) },
            { READOUT.FPUM_contents, new Tuple<int, int, int>(74, 10, 15) },
            { READOUT.FPUM_expansion, new Tuple<int, int, int>(94, 10, 6) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.GPF, new Tuple<int, int, int>(35, 11, 7) },
            { READOUT.GPF_contents, new Tuple<int, int, int>(43, 11, 15) },
            { READOUT.FPUA, new Tuple<int, int, int>(66, 11, 7) },
            { READOUT.FPUA_contents, new Tuple<int, int, int>(74, 11, 15) },
            { READOUT.FPUA_expansion, new Tuple<int, int, int>(93, 11, 11) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.GPG, new Tuple<int, int, int>(35, 12, 7) },
            { READOUT.GPG_contents, new Tuple<int, int, int>(43, 12, 15) },
            { READOUT.FPUB, new Tuple<int, int, int>(66, 12, 7) },
            { READOUT.FPUB_contents, new Tuple<int, int, int>(74, 12, 15) },
            { READOUT.FPUB_expansion, new Tuple<int, int, int>(93, 12, 11) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.COMPA, new Tuple<int, int, int>(4, 13, 7) },
            { READOUT.COMPA_contents, new Tuple<int, int, int>(12, 13, 15) },
            { READOUT.GPH, new Tuple<int, int, int>(35, 13, 7) },
            { READOUT.GPH_contents, new Tuple<int, int, int>(43, 13, 15) },
            { READOUT.FPUR, new Tuple<int, int, int>(66, 13, 7) },
            { READOUT.FPUR_contents, new Tuple<int, int, int>(74, 13, 15) },
            { READOUT.FPUR_expansion, new Tuple<int, int, int>(93, 13, 11) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.COMPB, new Tuple<int, int, int>(4, 14, 7) },
            { READOUT.COMPB_contents, new Tuple<int, int, int>(12, 14, 15) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.COMPR, new Tuple<int, int, int>(4, 15, 7) },
            { READOUT.COMPR_contents, new Tuple<int, int, int>(12, 15, 15) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.IADN, new Tuple<int, int, int>(4, 17, 7) },
            { READOUT.IADN_contents, new Tuple<int, int, int>(12, 17, 15) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.IADF, new Tuple<int, int, int>(4, 18, 7) },
            { READOUT.IADF_contents, new Tuple<int, int, int>(12, 18, 15) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.RBASE, new Tuple<int, int, int>(4, 21, 7) },
            { READOUT.RBASE_contents, new Tuple<int, int, int>(12, 21, 15) },
            { READOUT.MemoryContext1, new Tuple<int, int, int>(29, 21, 27) },
            { READOUT.WBASE, new Tuple<int, int, int>(58, 21, 7) },
            { READOUT.WBASE_contents, new Tuple<int, int, int>(66, 21, 15) },
            { READOUT.MemoryContext2, new Tuple<int, int, int>(83, 21, 27) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.ROFST, new Tuple<int, int, int>(4, 22, 7) },
            { READOUT.ROFST_contents, new Tuple<int, int, int>(12, 22, 15) },
            { READOUT.WOFST, new Tuple<int, int, int>(58, 22, 7) },
            { READOUT.WOFST_contents, new Tuple<int, int, int>(66, 22, 15) },
            ////////////////////////////////////////////////////////////////////////////
            { READOUT.RMEM, new Tuple<int, int, int>(4, 23, 7) },
            { READOUT.RMEM_contents, new Tuple<int, int, int>(12, 23, 15) },
            { READOUT.WMEM, new Tuple<int, int, int>(58, 23, 7) },
            { READOUT.WMEM_contents, new Tuple<int, int, int>(66, 23, 15) },


        };

        /// <summary>
        /// Mapping of register short names to visualizer colors
        /// </summary>
        private static Dictionary<string, ConsoleColor> register_name_to_color = new Dictionary<string, ConsoleColor>
        {
            {"FLAG", ConsoleColor.DarkGreen },
            {"EXE", ConsoleColor.DarkYellow },
            {"PC", ConsoleColor.White },
            {"SKIP", ConsoleColor.Gray },
            {"RTRN", ConsoleColor.White },
            {"LINK", ConsoleColor.Cyan },
            {"COMPA", ConsoleColor.DarkBlue},
            {"COMPB", ConsoleColor.DarkMagenta },
            {"COMPR", ConsoleColor.DarkGreen },
            {"IADN", ConsoleColor.Red },
            {"IADF", ConsoleColor.Green },
            {"GPA", ConsoleColor.DarkGray },
            {"GPB", ConsoleColor.Gray },
            {"GPC", ConsoleColor.White },
            {"GPD", ConsoleColor.Blue },
            {"GPE", ConsoleColor.Cyan },
            {"GPF", ConsoleColor.Green },
            {"GPG", ConsoleColor.Yellow },
            {"GPH", ConsoleColor.Red },
            {"ALUM", ConsoleColor.DarkMagenta },
            {"ALUA", ConsoleColor.DarkCyan },
            {"ALUB", ConsoleColor.DarkCyan },
            {"ALUR", ConsoleColor.Blue },
            {"FPUM", ConsoleColor.DarkMagenta },
            {"FPUA", ConsoleColor.DarkCyan },
            {"FPUB", ConsoleColor.DarkCyan },
            {"FPUR", ConsoleColor.Magenta },
            {"RBASE", ConsoleColor.Yellow },
            {"ROFST", ConsoleColor.Yellow },
            {"RMEM", ConsoleColor.Green },
            {"WBASE", ConsoleColor.Yellow },
            {"WOFST", ConsoleColor.Yellow },
            {"WMEM", ConsoleColor.Green },
            {"CurrentInstruction", ConsoleColor.Cyan },
            {"Header", ConsoleColor.DarkYellow },

        };

    }
}