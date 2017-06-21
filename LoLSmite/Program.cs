using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace LoLSmite
{
    class Program
    {
        static List<int> dwProcessList = new List<int>();

        static void Main(string[] args)
        {
            Console.Title = "LoL OpenSmite";
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n\t .--.                .-.              .      ");
            Console.WriteLine("\t:    :              (   )          o _|_     ");
            Console.WriteLine("\t|    |.,-.  .-. .--. `-. .--.--.   .  |  .-.");
            Console.WriteLine("\t:    ;|   )(.-' |  |(   )|  |  |   |  | (.-' ");
            Console.WriteLine("\t `--' |`-'  `--''  `-`-' '  '  `--' `-`-'`--'");
            Console.WriteLine("\t      |                                      ");
            Console.WriteLine("\t      '                                      \n");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Looking for \"League of Legends\" process...");
            while (true)
            {
                // Look for new League of Legends instance.
                foreach (Process p in Process.GetProcesses())
                {
                    if (p.ProcessName.Equals("League of Legends") && !dwProcessList.Contains(p.Id))
                    {
                        COpenSmite os = new COpenSmite(p.Id);
                        dwProcessList.Add(p.Id);
                        Console.WriteLine($"Found League of Legends! [PID:{p.Id}]");
                    }
                }

                // Remove dead processes
                foreach (int i in dwProcessList)
                {
                    try
                    {
                        Process.GetProcessById(i);
                    }
                    catch (ArgumentException ex)
                    {
                        if (ex.HResult == -2147024809)
                            dwProcessList.Remove(i);
                        break;
                    }
                }

                Thread.Sleep(2500);
            }
        }
    }
}
