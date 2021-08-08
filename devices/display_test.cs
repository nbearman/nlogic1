using System;

namespace nlogic_sim
{
    class display_test
    {
        public static void _Main()
        {
            VirtualDisplay d = new VirtualDisplay(90, 30);
            d.enable_display();

            int frame_counter = 0;

            while (true)
            {
                string frame_string = frame_counter.ToString();
                char[] chars = frame_string.ToCharArray();

                byte[] bytes = new byte[chars.Length];

                for (int i = 0; i < chars.Length; i++)
                {
                    bytes[i] = (byte)(chars[i]);
                }

                ((MMIO)d).write_memory(1, bytes);
                ((MMIO)d).write_memory(0, new byte[] { 1 });

                frame_counter++;

            }
        }
    }
}