using System;
using System.Diagnostics;
using System.Threading;

namespace nlogic_sim
{
    public class VirtualDisplay : MMIO
    {
        //MMIO layout
        //0x00  : commit active back buffer
        //0x01 - 0x01 + buffersize: active back buffer

        public const byte COMMIT = 0x00;
        public const byte BUFFER_BASE = 0x01;

        //processor and virtual display threads both need access to memory banks
        //processor during calls to write_memory() and display while drawing
        private readonly object buffer0_mutex = new object();
        private readonly object buffer1_mutex = new object();
        private readonly object buffer2_mutex = new object();

        //pointer lock that guards the buffer pointers; only one thread should access these
        private readonly object buffer_pointer_mutex = new object();

        //array of frame buffers
        //protected by buffer mutexes
        private byte[][] buffers;

        //frame buffer pointers (hold index into buffers[] for which bank each buffer is using)
        //protected by buffer pointer mutex
        int front;
        int ready_back;
        int active_back;

        //buffer parameters
        //unprotected; used only by constructor, then processor thread
        readonly int buffer_size;
        int width;
        int height;

        public VirtualDisplay(int width, int height)
        {
            //no locks needed; display thread not started yet

            buffer_size = width * height;
            this.width = width;
            this.height = height;

            //initalize the buffers
            buffers = new byte[3][];
            buffers[0] = new byte[buffer_size];
            buffers[1] = new byte[buffer_size];
            buffers[2] = new byte[buffer_size];

            //point each buffer to a memory bank
            front = 0;
            ready_back = 1;
            active_back = 2;
        }

        public Thread enable_display()
        {
            Console.CursorVisible = false;
            Thread p = new Thread(display_thread);
            p.Start();
            return p;
        }

        private void display_thread()
        {
            //thread should loop indefinitely
            while (true)
            {
                Thread.Sleep(16);

                //draw a frame
                draw_frame();

                //change out the buffers
                swap_buffers();
            }
        }

        private void swap_buffers()
        {
            lock (buffer_pointer_mutex)
            {
                //the most recently finished buffer becomes the front buffer
                int t = front;
                front = ready_back;

                //most recently finished buffer now points to the just-used front buffer
                ready_back = t;

                //active back buffer is not affected
            }
        }

        private void commit()
        {
            lock (buffer_pointer_mutex)
            {
                //switch the back buffers
                int t = ready_back;
                ready_back = active_back;
                active_back = ready_back;

                //the active back buffer is now overwriting the previously committed buffer
            }
        }

        //store data into the active back frame buffer
        //buffer_address is the target offset within the frame buffer
        private void write_frame_buffer(uint buffer_address, byte[] data)
        {
            int destination = 0;

            //lock can be released after grabbing the pointer because only the processor thread
            //(this thread) can change this pointer
            lock (buffer_pointer_mutex)
            {
                destination = active_back;
            }

            if (destination == 0)
            {
                lock (buffer0_mutex)
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        buffers[0][buffer_address + i] = data[i];
                    }
                }
            }

            if (destination == 1)
            {
                lock (buffer1_mutex)
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        buffers[1][buffer_address + i] = data[i];
                    }
                }
            }

            if (destination == 2)
            {
                lock (buffer2_mutex)
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        buffers[2][buffer_address + i] = data[i];
                    }
                }
            }
        }

        uint MMIO.get_size()
        {
            return (uint)buffer_size;
        }

        void MMIO.write_memory(uint address, byte[] data)
        {

            //if non-zero data is written to the COMMIT address, swap the buffers
            if (address == COMMIT)
            {
                if (Utility.uint32_from_byte_array(data) != 0)
                {
                    commit();
                }
            }

            else
            {

                //no address may be outside the range of the frame buffer
                //(buffersize + 1, extra byte for the COMMIT signal)
                uint max_access = address + (uint)data.Length;
                Debug.Assert(max_access < (buffer_size + 1));

                //translate the address into a frame buffer offset
                address -= 1; //subtract one for the COMMIT byte which isn't part of the frame buffer

                //write to the frame buffer
                write_frame_buffer(address, data);
            }
        }

        byte[] MMIO.read_memory(uint address, uint length)
        {
            //the processor may not read from the virtual display
            //Debug.Assert(false);

            byte[] result = new byte[length];
            return result;
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

        private void draw_frame()
        {
            int front_buffer = 0;

            //grab the front buffer pointer; only the display thread
            //(this thread) can change it, so release the lock
            lock (buffer_pointer_mutex)
            {
                front_buffer = front;
            }

            if (front_buffer == 0)
            {
                lock (buffer0_mutex)
                {
                    draw_buffer(buffers[0]);
                }
            }

            if (front_buffer == 1)
            {
                lock (buffer1_mutex)
                {
                    draw_buffer(buffers[1]);
                }
            }

            if (front_buffer == 2)
            {
                lock (buffer2_mutex)
                {
                    draw_buffer(buffers[2]);
                }
            }
        }

        private void draw_buffer(byte[] buffer)
        {
            Console.SetCursorPosition(0, 0);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Console.Write((char)buffer[(y * height) + x]);
                }

                if (y < height - 1)
                    Console.WriteLine();
            }
        }

        public void initialize(SimulationEnvironment environment)
        {
            //no setup required
            return;
        }
    }
}