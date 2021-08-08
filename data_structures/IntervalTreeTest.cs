using System;

namespace nlogic_sim
{
    class test_program
    {
        static void _Main()
        {
            IntervalTree<int, string> tree = new IntervalTree<int, string>();

            tree.insert(10, 100, "ten to one hundred");
            tree.insert(-1000, 0, "negative");
            tree.insert(0, 10, "zero to ten");

            Random random = new Random();
            for (int i = 0; i < 15; i++)
            {
                int r = random.Next(-100, 99);
                Console.WriteLine(r + ": " + tree.get_data(r));
            }

            Console.ReadKey();
        }
    }
}