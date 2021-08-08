using System;
using System.Collections.Generic;

namespace nlogic_sim
{
    public partial class Processor
    {
        public enum ALU_MODE
        {
            NoOp = 0,
            Add = 1,
            Multiply = 2,
            Subtract = 3,
            Divide = 4,
            ShiftLeft = 5,
            ShiftRight = 6,
            OR = 7,
            AND = 8,
            XOR = 9,
            NAND = 10,
            NOR = 11,
        }

        public enum FPU_MODE
        {
            NoOp = 0,
            Add = 1,
            Multiply = 2,
            Subtract = 3,
            Divide = 4,
        }

        public const byte BREAKPOINT = 0x7B;

        public const byte IMM = 0x00;
        public const byte FLAG = 0x80;
        public const byte EXE = 0x81;
        public const byte PC = 0x82;
        public const byte ALUM = 0x83;
        public const byte ALUA = 0x84;
        public const byte ALUB = 0x85;
        public const byte ALUR = 0x86;
        public const byte FPUM = 0x87;
        public const byte FPUA = 0x88;
        public const byte FPUB = 0x89;
        public const byte FPUR = 0x8A;
        public const byte RBASE = 0x8B;
        public const byte ROFST = 0x8C;
        public const byte RMEM = 0x8D;
        public const byte WBASE = 0x8E;
        public const byte WOFST = 0x8F;
        public const byte WMEM = 0x90;
        public const byte GPA = 0x91;
        public const byte GPB = 0x92;
        public const byte GPC = 0x93;
        public const byte GPD = 0x94;
        public const byte GPE = 0x95;
        public const byte GPF = 0x96;
        public const byte GPG = 0x97;
        public const byte GPH = 0x98;
        public const byte COMPA = 0x99;
        public const byte COMPB = 0x9A;
        public const byte COMPR = 0x9B;
        public const byte IADN = 0x9C;
        public const byte IADF = 0x9D;
        public const byte LINK = 0x9E;
        public const byte SKIP = 0x9F;
        public const byte RTRN = 0xA0;
        public const byte DMEM = 0xC0;

        public struct LastStateCache
        {
            public uint PC;
            public uint EXE;
            //the contents of the cached register
            public uint stored_register_contents;
            //the register that was cached before the last instruction
            public byte stored_register;
        }

        /// <summary>
        /// The list of registers which must be restored when if an instruction that
        /// writes to them is retried.
        /// </summary>
        //public static List<byte> cacheable_registers = new List<byte>(new byte[] 
        //{
        //    COMPA,
        //    COMPB,
        //    RBASE,
        //    ROFST,
        //    WBASE,
        //    WOFST,
        //});
    }


    //TODO should probably move this into the processor class
    /// <summary>
    /// List of flags in the status register (FLAG); the enum value is the number
    /// of bits right of the most significant bit where the flag's corresponding bit is
    /// </summary>
    public enum Flags
    {
        UNLOCKED = 0,
        DISABLED = 1,
        DELAY = 2,
        RETRY = 3,
        KERNEL = 4,
        USER_DISABLED = 5,
        USER_DELAY = 6,
    }

    public struct Interrupt
    {
        public uint channel;
        public bool retry;
        public bool kernel;
    }

    /// <summary>
    /// Interface for a memory-mapped input-output device. MMIO devices
    /// are managed by the simulation environment and are addressable by
    /// the processor, just like memory. MMIO devices must know how much
    /// of the address space they require (in bytes) and provide this with
    /// get_size(). MMIO devices must also handle being written to and
    /// being read. The address provided to calls of write_memory() and
    /// read_memory() will be in the MMIO device's virtual address space
    /// (addresses will range from 0 to this.get_size()), and therefore
    /// the MMIO device does not need to handle address translation.
    /// </summary>
    public interface MMIO
    {
        /// <summary>
        /// Called by the simulation environment before starting the simulation to
        /// give the MMIO device access to the environment for any set up it requires.
        /// </summary>
        void initialize(SimulationEnvironment environment);

        /// <summary>
        /// Get the size, in bytes, of the address space required by this device.
        /// </summary>
        /// <returns>The size of the address space required by this device in bytes.</returns>
        uint get_size();


        /// <summary>
        /// Write the given data array to the device at the given virtual address.
        /// </summary>
        /// <param name="address">The address start address of the write in the device's address space (no translation required)</param>
        /// <param name="data">The data to be written</param>
        void write_memory(uint address, byte[] data);


        /// <summary>
        /// Read the given length of data starting at the given address in the device's virtual address space.
        /// </summary>
        /// <param name="address">The address start address of the read in the device's address space (no translation required)</param>
        /// <param name="length">The number of bytes to be read</param>
        /// <returns></returns>
        byte[] read_memory(uint address, uint length);

        /// <summary>
        /// Read a single byte of data from the device.
        /// </summary>
        /// <param name="address">The address of the byte to read in the device's address space</param>
        /// <returns></returns>
        byte read_byte(uint address);

        void write_byte(uint address, byte data);
    }

    public interface HardwareInterrupter
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="signal_callback">Method that takes two booleans: RETRY and KERNEL,
        /// which use the RETRY and KERNEL flag, respectively, on the interrupt
        /// </param>
        void register_signal_callback(Action<bool, bool> signal_callback);
    }

    /// <summary>
    /// Interface through which the processor communicates with the environment.
    /// </summary>
    public interface I_Environment
    {
        byte[] read_address(uint address, uint length, bool debug_visualizer_read=false);
        void write_address(uint address, byte[] data_array);
        void register_signal_callback(Action<Interrupt> signal_callback);
        void signal_kernel_mode();

        //TODO this is necessary only for the visualizer
        //refactor visualizer to be part of the environment and remove this
        byte[] get_memory();
    }
}