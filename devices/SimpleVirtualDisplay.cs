using System;
using System.Threading;
using System.Diagnostics;

namespace nlogic_sim
{
    public class SimpleVirtualDisplay : MMIO
    {

        //address to write to for clear
        public const byte CLEAR = 0x00;

        //address to write new characters to
        public const byte TEXT = 0x01;

        uint MMIO.get_size()
        {
            //1 for clear byte
            //4 for up to 4 bytes in a single write
            return 5;
        }

        void MMIO.write_memory(uint address, byte[] data)
        {
            if (address == CLEAR)
            {
                Console.Clear();
            }

            else if (address == TEXT)
            {
                foreach (byte b in data)
                {
                    Console.Write((char)b);
                }
            }
        }

        byte[] MMIO.read_memory(uint address, uint length)
        {
            return new byte[length];
        }

        byte MMIO.read_byte(uint address)
        {
            throw new NotImplementedException();
            return 0;
        }

        void MMIO.write_byte(uint address, byte data)
        {
            throw new NotImplementedException();
        }

        public void initialize(SimulationEnvironment environment)
        {
            //no setup needed
            return;
        }
    }
}