using System;
using System.Diagnostics;
using System.Linq;

namespace nlogic_sim
{
    public class VirtualDisk : MMIO
    {
        public const int BLOCK_SIZE = 4096;
        public const int PAGE_SIZE = 4096;

        //MMIO layout
        //0x00 - 0x03: target physical page
        //0x04 - 0x07: target disk block
        //0x08 - 0x0B: read / write mode
        //0x0C - 0x0F: initiate transfer

        public const byte PHYSICAL_PAGE = 0x00;
        public const byte DISK_BLOCK = 0x04;
        public const byte MODE = 0x08; //0 == read from disk to memory, else write from memory to disk
        public const byte INITIATE = 0x0C;

        private byte[] memory = new byte[12];

        private byte[] environment_memory;

        private string disk_folder_path;

        public VirtualDisk(string disk_folder_path)
        {
            this.disk_folder_path = disk_folder_path;
        }

        private void begin_transfer()
        {
            byte[] seg = new ArraySegment<byte>(this.memory, PHYSICAL_PAGE, 4).ToArray();
            uint page = Utility.uint32_from_byte_array(seg);
            seg = new ArraySegment<byte>(this.memory, DISK_BLOCK, 4).ToArray();
            uint block = Utility.uint32_from_byte_array(seg);
            seg = new ArraySegment<byte>(this.memory, MODE, 4).ToArray();
            uint mode = Utility.uint32_from_byte_array(seg);

            string filename = String.Format("{0}/{1:D5}.txt", disk_folder_path, block);

            int page_address = PAGE_SIZE * (int)page;

            if (mode == 0)
            {
                //read from disk to memory

                //read disk block file
                string block_contents = File_Input.get_file_contents(filename);

                string[] split = block_contents.Split(new char[] { ' ' }, BLOCK_SIZE);

                //overwrite environment memory at page
                for (int i = 0; i < PAGE_SIZE; i++)
                {
                    int memory_address = page_address + i;

                    //if the split is shorter than the page, fill the rest with 0s
                    if (i >= split.Length)
                    {
                        this.environment_memory[memory_address] = 0;
                    }
                    else
                    {
                        //convert the string to a byte
                        bool parse_success = byte.TryParse(
                                    split[i],
                                    System.Globalization.NumberStyles.HexNumber,
                                    null,
                                    out byte converted);

                        Debug.Assert(parse_success, "Could not parse byte while reading disk: " + split[i]);
                        //write the byte to memory
                        this.environment_memory[memory_address] = converted;
                    }
                }

                return;
            }

            else
            {
                //write to disk from memory
                byte[] page_bytes = new ArraySegment<byte>(this.environment_memory, page_address, PAGE_SIZE).ToArray();
                string byte_string = Utility.byte_array_string(page_bytes);
                File_Input.write_file(filename, byte_string);

                return;
            }
        }

        public uint get_size()
        {
            return 16;
        }

        public byte read_byte(uint address)
        {
            if (address < INITIATE)
                return this.memory[address];
            else
                return 0;
        }

        public byte[] read_memory(uint address, uint length)
        {
            throw new NotImplementedException();
        }

        public void write_byte(uint address, byte data)
        {
            if (address >= INITIATE + 4)
                throw new NotImplementedException();
            else if (address >= INITIATE)
                //TODO this should only happen when writing the first byte; otherwise, we transfer 4 times
                this.begin_transfer();
            else
                this.memory[address] = data;
        }

        public void write_memory(uint address, byte[] data)
        {
            throw new NotImplementedException();
        }

        public void initialize(SimulationEnvironment environment)
        {
            this.environment_memory = environment.get_memory();
        }
    }
}