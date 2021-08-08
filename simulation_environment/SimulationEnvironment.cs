using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace nlogic_sim
{
    /// <summary>
    /// The simulation environment is the object that manages the processor and all other devices
    /// the processor is connected to. The simulation environment is responsible for processing
    /// reads and writes from the processor as well as transmitting signals from devices to the
    /// processor. The simulation environment handles address translation from physical address
    /// of reads and writes from the processor to the local address of the MMIO device.
    /// </summary>
    public class SimulationEnvironment : I_Environment
    {
        //logging information
        private const int MAX_LOG_SIZE = 1000;
        private bool logging_enabled = false;
        private List<string> state_logs = new List<string>(MAX_LOG_SIZE);

        /// <summary>
        /// An interval tree which maps uint addresses to (uint base address, MMIO device) tuples
        /// Indexed by an address, returns a tuple of the base address of that range and the
        /// device whose range that address belongs to.
        /// Storing the base address with the device this way makes translating addresses from CPU space
        /// to local MMIO device space simple for the environment, and removes the need for an additional
        /// data structure to hold the base address of each attached device.
        /// </summary>
        private IntervalTree<uint, Tuple<uint, MMIO>> MMIO_devices_by_address;

        private MemoryManagementUnit MMU;

        //enable the trap on the processor
        public bool trap_enabled = false;

        //physical memory
        public byte[] memory;

        //the processor managed by this simulated environment
        private Processor processor;

        //true if the processor has registered its signal callback with the environments
        private bool signal_callback_registered = false;
        //the callback to raise signals for the processor
        private Action<Interrupt> processor_signal_callback;

        //queue to store interruptions received from MMIO devices since the last
        //time interruptions were forwarded to the processor
        //accessed by the environment thread as well as threads running on MMIO devices,
        //so it must be protected by a lock
        private object signal_queue_mutex = new object();
        private Queue<Interrupt> signal_queue = new Queue<Interrupt>();

        public SimulationEnvironment(uint memory_size, byte[] initial_memory, List<MMIO> MMIO_devices)
        {
            //create memory
            memory = new byte[memory_size];

            //create the MMU
            this.MMU = new MemoryManagementUnit(this.memory);

            //create the MMIO devices
            this.MMIO_devices_by_address = new IntervalTree<uint, Tuple<uint, MMIO>>();
            MMIO_devices.Insert(0, this.MMU);
            //TODO setting the first MMIO device address to arbitrary constant for now
            initialize_MMIO(MMIO_devices, 0xFF000000);

            //initialize the hardware interrupters
            initialize_hardware_interrupters(new HardwareInterrupter[] { this.MMU });

            //create the processor
            this.processor = new Processor(this);

            //initialize memory
            this.write_address(0, initial_memory);

            //cannot begin execution unless signal callback has been registered
            //if this assertion fails, the processor is not configured to correctly
            //communicate with the simulation environment
            Debug.Assert(this.signal_callback_registered);
        }

        public void enable_logging()
        {
            this.logging_enabled = true;
        }

        /// <summary>
        /// Begin simluating the processor
        /// </summary>
        /// <param name="visualizer_enabled">Whether or not the visualizer should be used</param>
        /// <param name="halt_status">The value of the FLAG register which will cause the simulation to end.</param>
        public void run(bool visualizer_enabled, uint halt_status)
        {
            if (visualizer_enabled)
            {
                this.processor.initialize_visualizer();
            }

            uint cycle_status = 0;
            while (cycle_status != halt_status)
            {
                //enable the visualizer if a breakpoint was hit
                if (cycle_status == Processor.BREAKPOINT && !visualizer_enabled)
                {
                    Console.Clear();
                    this.processor.initialize_visualizer();
                    visualizer_enabled = true;
                }

                //save the state of the processor if logging is enabled
                if (logging_enabled)
                {
                    this.log_state();
                }

                //display the visualizer if it is enabled
                if (visualizer_enabled)
                {
                    this.processor.print_current_state();
                    Console.ReadKey();
                }

                //send outstanding interrupts to the processor
                this.resolve_signal_queue();

                //clear faults on the MMU when the processor cycles
                MMU.clear_fault();

                //cycle the processor
                cycle_status = this.processor.cycle();
            }

            //print the end state of the processor if the visualizer is enabled
            if (visualizer_enabled)
            {
                this.processor.print_current_state();
                Console.WriteLine("Press enter.");
                Console.ReadLine();
            }

        }

        /// <summary>
        /// Returns the data from the given physical address
        /// The data returned is not necessarily from memory;
        /// it could come from MMIO devices
        /// </summary>
        /// <param name="address">Address to begin reading at</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Returns an array of size length containing the data from the given address</returns>
        public byte[] read_address(uint address, uint length, bool debug_visualizer_read=false)
        {
            byte[] result = new byte[length];

            VirtualAddressTranslation[] translations = get_translations_for_range(address, length, false, debug_visualizer_read);
            if (translations == null)
            {
                //the translation failed
                //return garbage
                //or return null as a sentinel if this read is for the visualizer
                return debug_visualizer_read ? null : result;
            }

            int current_page = 0;
            uint page_offset = 0;
            for (int virtual_offset = 0; virtual_offset < length; virtual_offset++)
            {
                //if in the last translation range, don't need to check if we're in the next range
                if (current_page != translations.Length - 1)
                {
                    if (address + virtual_offset >= translations[current_page + 1].virtual_address)
                    {
                        current_page += 1;
                        page_offset = 0;
                    }
                }

                uint physical_address = translations[current_page].physical_address + page_offset;

                if (physical_address >= memory.Length)
                {
                    //address is beyond physical memory, check MMIO devices

                    //memory previewer cannot read from MMIO devices, so return null
                    //TODO look into changing this, possibly only for some types of devices?
                    if (debug_visualizer_read)
                        return null;

                    //get the target device and base address
                    Tuple<uint, MMIO> target_device = this.MMIO_devices_by_address.get_data(physical_address);
                    uint base_address = target_device.Item1;
                    MMIO device = target_device.Item2;

                    //translate the processor's requested address to the address space of the device
                    uint translated_address = physical_address - base_address;

                    //read the data from the device at the translated address
                    result[virtual_offset] = device.read_byte(translated_address);
                }
                else
                {
                    result[virtual_offset] = memory[physical_address];
                }

                //increment the offset into the current page
                page_offset += 1;
            }

            return result;

        }

        /// <summary>
        /// Writes the given data to the given virtual address,
        /// which may not necessarily be in memory
        /// </summary>
        /// TODO update this summary and the one for read_address() to reflect new page-aware changes
        /// <param name="address">Starting virtual address to write the data to</param>
        /// <param name="data_array">Array of bytes to write at the address</param>
        public void write_address(uint address, byte[] data_array)
        {
            uint length = (uint)data_array.Length;
            VirtualAddressTranslation[] translations = get_translations_for_range(address, length, true);
            if (translations == null)
            {
                //the translation failed
                //do nothing
                return;
            }

            int current_page = 0;
            uint page_offset = 0;
            for (int virtual_offset = 0; virtual_offset < length; virtual_offset++)
            {
                //if in the last translation range, don't need to check if we're in the next range
                if (current_page != translations.Length - 1)
                {
                    if (address + virtual_offset >= translations[current_page + 1].virtual_address)
                    {
                        current_page += 1;
                        page_offset = 0;
                    }
                }

                uint physical_address = translations[current_page].physical_address + page_offset;

                if (physical_address >= memory.Length)
                {
                    //address is beyond physical memory, check MMIO devices

                    //get the target device and base address
                    Tuple<uint, MMIO> target_device = this.MMIO_devices_by_address.get_data(physical_address);
                    uint base_address = target_device.Item1;
                    MMIO device = target_device.Item2;

                    //translate the processor's requested address to the address space of the device
                    uint translated_address = physical_address - base_address;

                    //write the data to the device at the translated address
                    device.write_byte(translated_address, data_array[virtual_offset]);
                }
                else
                {
                    memory[physical_address] = data_array[virtual_offset];
                }

                //increment the offset into the current page
                page_offset += 1;
            }
        }

        /// <summary>
        /// Called by the processor to supply the environment 
        /// with a way to send a signal back to the processor.
        /// The signal callback can be used by the environment
        /// (or devices in the environment) to send signals,
        /// like interrupts, to the processor.
        /// </summary>
        /// <param name="signal_callback">
        /// A method which takes an Interrupt struct as a parameter.
        /// Invoking signal_callback(interrupt) will send the interrupt
        /// the processor with the given flags on the given channel.
        /// </param>
        public void register_signal_callback(Action<Interrupt> signal_callback)
        {
            this.processor_signal_callback = signal_callback;
            this.signal_callback_registered = true;
        }

        /// <summary>
        /// Called by the processor to set the environment (the MMU) to kernel mode
        /// </summary>
        public void signal_kernel_mode()
        {
            //change the MMU to the queued active page directory (the kernel page directory)
            this.MMU.swap_directories();
        }

        public byte[] get_memory()
        {
            return this.memory;
        }

        /// <summary>
        /// Return an array of translation results from the MMU. If any of the translations
        /// fail (page fault), returns null.
        /// </summary>
        /// <param name="base_address">Starting address of the memory access</param>
        /// <param name="length">Total length of the memory access, in bytes</param>
        /// <param name="write">True if the memory access will be a write, false if only a read</param>
        /// <returns></returns>
        private VirtualAddressTranslation[] get_translations_for_range(uint base_address, uint length, bool write, bool debug_visualizer_read=false)
        {
            Debug.Assert(!(write && debug_visualizer_read), "cannot use debug_visualizer_read to alter memory");

            List<uint> address_thresholds = new List<uint>();

            address_thresholds.Add(base_address);

            long long_first_boundary = (((long)base_address / MemoryManagementUnit.PAGE_SIZE) + 1) * MemoryManagementUnit.PAGE_SIZE;
            Debug.Assert(long_first_boundary < uint.MaxValue, "overflow: cannot calculate page boundaries when accessing past 32 bit address");
            uint first_boundary = (uint)long_first_boundary;

            uint boundary_counter = first_boundary;
            while (boundary_counter < base_address + length)
            {
                address_thresholds.Add(boundary_counter);
                boundary_counter += MemoryManagementUnit.PAGE_SIZE;
            }

            List<VirtualAddressTranslation> result = new List<VirtualAddressTranslation>();

            for (int i = 0; i < address_thresholds.Count; i++)
            {
                uint virtual_address = address_thresholds[i];
                uint physical_address;
                bool success;
                if (debug_visualizer_read)
                    success = this.MMU.translate_address_for_preview(virtual_address, out physical_address);
                else
                    success = this.MMU.translate_address(virtual_address, write, out physical_address);

                if (!success)
                {
                    return null;
                }
                result.Add(new VirtualAddressTranslation(virtual_address, physical_address, success));
            }

            return result.ToArray();
        }

        private struct VirtualAddressTranslation
        {
            public uint virtual_address;
            public uint physical_address;
            public bool valid;

            public VirtualAddressTranslation(uint virtual_address, uint physical_address, bool valid)
            {
                this.virtual_address = virtual_address;
                this.physical_address = physical_address;
                this.valid = valid;
            }
        }

        /// <summary>
        /// Set up all the MMIO devices in the environment
        /// </summary>
        private void initialize_MMIO(List<MMIO> MMIO_devices, uint base_address)
        {
            if (MMIO_devices == null)
            {
                return;
            }

            //assign base addresses to all MMIO devices

            //for each device
            for (int i = 0; i < MMIO_devices.Count; i++)
            {
                //run the device's setup
                MMIO_devices[i].initialize(this);

                //ensure that MMIO devices aren't mapped outside of 32 bit address space
                Debug.Assert(base_address < 0xFFFFFFFF, "MMIO address beyond addressable range");

                //get size to calculate base address for next device
                uint next_base_address = base_address + MMIO_devices[i].get_size();

                //insert the device into the interval tree
                Tuple<uint, MMIO> base_address_device_pair = new Tuple<uint, MMIO>(base_address, MMIO_devices[i]);
                this.MMIO_devices_by_address.insert(base_address, next_base_address, base_address_device_pair);

                //update the base address to be used next
                base_address = next_base_address;
            }

        }

        private void initialize_hardware_interrupters(HardwareInterrupter[] interrupter_devices)
        {
            //the environment cannot support more interrupters than there are channels
            //TODO change to get the number of supported channels from the processor
            Debug.Assert(interrupter_devices.Length < 25);

            //give each interrupter a callback to use on their own thread
            //which raises a signal on their assigned channel
            for (uint channel = 0; channel < interrupter_devices.Length; channel++)
            {
                //create a method that will raise a signal on this device's assigned channel

                //this method is defined on a hidden functor so that it can be handed off to
                //the interrupter device as a void function; this prevents the interrupter
                //class from having to keep track of what channel to call signals on and
                //prevents interrupters from raising signals on the wrong channel

                signal_functor custom_signal_callback_functor = new signal_functor(this, channel);
                Action<bool, bool> custom_signal_callback = new Action<bool, bool>(custom_signal_callback_functor.signal);
                interrupter_devices[channel].register_signal_callback(custom_signal_callback);
            }
        }

        /// <summary>
        /// Send all signals built up in the queue to the processor,
        /// removing them from the queue as they are processed
        /// </summary>
        private void resolve_signal_queue()
        {
            //the signal queue is accessible by interrupters' threads
            //so it is protected with a lock
            //hold the lock while sending interrupts to the processor
            //this means that interrupters could be blocked at some point
            //between processor cycles
            lock (signal_queue_mutex)
            {
                while (signal_queue.Count > 0)
                {
                    Interrupt signal = signal_queue.Dequeue();
                    this.processor_signal_callback(signal);
                }
            }

            return;
        }

        /// <summary>
        /// The target method of the customized functors' signal() methods.
        /// This method is responsible for processing a request from a call
        /// to signal() (possibly on a non-main thread) by adding it to the
        /// queue of signals the environment will raise before the next cycle
        /// </summary>
        /// <param name="interrupt_signal">The interrupt struct to send to the processor</param>
        private void queue_signal(Interrupt interrupt_signal)
        {
            //add a signal to the signal queue
            //grab the lock, first, because this method is called directly
            //by interrupters, possible on their own threads
            lock (this.signal_queue_mutex)
            {
                this.signal_queue.Enqueue(interrupt_signal);
            }

            return;
        }

        private struct signal_functor
        {
            private uint channel;
            private SimulationEnvironment environment;

            /// <summary>
            /// Callback to be used by the hardware interrupter on its own thread to raise an interrupt
            /// signal on the appropriate channel with the given additional flags
            /// 
            /// Only "retry" and "kernel only" flags can be specified by interrupters
            /// Other flags on the interrupt can only be set directly by the processor
            /// </summary>
            /// <param name="retry">True if the RETRY flag should be used on the interrupt</param>
            /// <param name="kernel">True if the KERNEL flag should be used on the interrupt</param>
            public void signal(bool retry, bool kernel)
            {
                Interrupt interrupt_signal = new Interrupt();
                interrupt_signal.channel = this.channel;
                interrupt_signal.retry = retry;
                interrupt_signal.kernel = kernel;

                this.environment.queue_signal(interrupt_signal);
            }

            public signal_functor(SimulationEnvironment environment, uint channel)
            {
                this.environment = environment;
                this.channel = channel;
            }
        }

        /// <summary>
        /// Logs the current state of all the processor's registers to this instance's state logs
        /// </summary>
        private void log_state()
        {
            string state_string = "";
            foreach (Register_32 register in this.processor.registers.Values)
            {
                string value = Utility.byte_array_string(register.data_array);
                string name = register.name_short;
                state_string += name + " " + value + "\n";
            }
            state_string += "\n";
            this.state_logs.Add(state_string);
        }

        /// <summary>
        /// Return the state logs as a string
        /// </summary>
        public string get_log()
        {
            string log_string = "";
            for (int i = 0; i < this.state_logs.Count; i++)
            {
                log_string += String.Format("#{0}\n", i);
                log_string += this.state_logs[i];
            }
            return log_string;
        }
    }
}