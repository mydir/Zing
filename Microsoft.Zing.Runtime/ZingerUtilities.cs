﻿using System;
using System.Threading;
using System.IO;

namespace Microsoft.Zing
{
    public class ZingerUtilities
    {
        public static Random rand = new Random(DateTime.Now.Millisecond);

        public static void PrintSuccessMessage(string message)
        {
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ForegroundColor = prevColor;
        }

        public static void PrintErrorMessage(string message)
        {
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = prevColor;
        }

        public static void PrintMessage(string message)
        {
            if (ZingerConfiguration.PrintStats)
            {
                var prevColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine(message);
                Console.ForegroundColor = prevColor;
            }
        }

        public static void ZingerTimeOut(object obj)
        {
            ZingerUtilities.PrintMessage("");
            ZingerUtilities.PrintMessage(String.Format("--Zinger Timed Out --"));
            ZingerUtilities.PrintMessage(String.Format("--Final Stats --"));
            ZingerStats.PrintPeriodicStats();
            Environment.Exit((int)ZingerResult.ZingerTimeOut);
        }

        private static System.Threading.Timer TimeOutTimer;
        public static void StartTimeOut()
        {
            TimerCallback tcb = ZingerTimeOut;
            TimeOutTimer = new Timer(tcb, null, ZingerConfiguration.Timeout * 1000, ZingerConfiguration.Timeout * 1000);
        }
    }

    public class ZingerStats
    {
        public static Int64 NumOfStatesVisited = 0;
        public static Int64 NumOfTransitionExplored = 0;
        public static Int64 NumOfFrontiers = 0;
        public static Int64 NumOfSchedulesExplored = 0;
        public static Int64 MaxDepth = 0;

        public static DateTime startTime = DateTime.Now;

        public static void PrintFinalStats()
        {
            var finishTime = DateTime.Now;
            var elapsedTime = finishTime.Subtract(startTime);
            ZingerUtilities.PrintErrorMessage(String.Format("{0} distinct states explored", NumOfStatesVisited));


            if (ZingerConfiguration.PrintStats)
            {
                ZingerUtilities.PrintMessage(String.Format("Maximum Depth Explored : {0}", MaxDepth));

                if (ZingerConfiguration.DoRandomSampling || ZingerConfiguration.DoLivenessSampling)
                    ZingerUtilities.PrintMessage(String.Format("Number of Schedules Explored : {0}", NumOfSchedulesExplored));

                ZingerUtilities.PrintMessage(String.Format("Elapsed time : {0:00}:{1:00}:{2:00}", (int)elapsedTime.TotalHours, (int)elapsedTime.TotalMinutes, (int)elapsedTime.TotalSeconds));
                ZingerUtilities.PrintMessage("Memory Stats:");
                ZingerUtilities.PrintMessage(String.Format("Peak Virtual Memory Size: {0} MB",
                    (double)System.Diagnostics.Process.GetCurrentProcess().PeakVirtualMemorySize64 / (1 << 20)));
                ZingerUtilities.PrintMessage(String.Format("Peak Paged Memory Size  : {0} MB",
                    (double)System.Diagnostics.Process.GetCurrentProcess().PeakPagedMemorySize64 / (1 << 20)));
                ZingerUtilities.PrintMessage(String.Format("Peak Working Set Size   : {0} MB",
                    (double)System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 / (1 << 20)));
            }
        }

        public static void PrintPeriodicStats()
        {
            if (ZingerConfiguration.PrintStats)
            {
                var finishTime = DateTime.Now;
                var elapsedTime = finishTime.Subtract(startTime);
                if (ZingerConfiguration.DoDelayBounding)
                {
                    ZingerUtilities.PrintMessage(String.Format("Delay Bound {0}", ZingerConfiguration.zBoundedSearch.IterativeCutoff));
                }
                else if (ZingerConfiguration.DoPreemptionBounding)
                {
                    ZingerUtilities.PrintMessage(String.Format("Preemption Bound {0}", ZingerConfiguration.zBoundedSearch.IterativeCutoff));
                }
                else
                {
                    ZingerUtilities.PrintMessage(String.Format("Depth Bound {0}", ZingerConfiguration.zBoundedSearch.IterativeCutoff));
                }
                if(!(ZingerConfiguration.DoRandomSampling || ZingerConfiguration.DoLivenessSampling))
                    ZingerUtilities.PrintMessage(String.Format("No. of Frontiers {0}", NumOfFrontiers));

                ZingerUtilities.PrintMessage(String.Format("No. of Distinct States {0}", NumOfStatesVisited));
                ZingerUtilities.PrintMessage(String.Format("Total Transitions: {0}", NumOfTransitionExplored));
                ZingerUtilities.PrintMessage(String.Format("Maximum Depth Explored : {0}", MaxDepth));

                if (ZingerConfiguration.DoRandomSampling || ZingerConfiguration.DoLivenessSampling)
                    ZingerUtilities.PrintMessage(String.Format("Number of Schedules Explored : {0}", NumOfSchedulesExplored));

                ZingerUtilities.PrintMessage(String.Format("Total Exploration time so far = {0}", elapsedTime.ToString()));
                ZingerUtilities.PrintMessage(String.Format("Peak / Current Paged Mem Usage : {0} M/{1} M", System.Diagnostics.Process.GetCurrentProcess().PeakPagedMemorySize64 / (1 << 20), System.Diagnostics.Process.GetCurrentProcess().PagedMemorySize64 / (1 << 20)));
                ZingerUtilities.PrintMessage(String.Format("Peak / Current working set size: {0} M/{1} M", System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 / (1 << 20), System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / (1 << 20)));

                Console.WriteLine();
            }
        }

        public static void PeriodicTimerFunction(object obj)
        {
            ZingerUtilities.PrintMessage("");
            ZingerUtilities.PrintMessage(String.Format("--Periodic Stats --"));
            PrintPeriodicStats();
        }

        
        public static void IncrementStatesCount()
        {
            Interlocked.Increment(ref NumOfStatesVisited);
        }

        public static void IncrementTransitionsCount()
        {
            Interlocked.Increment(ref NumOfTransitionExplored);
        }

        public static void IncrementFrontiersCount()
        {
            Interlocked.Increment(ref NumOfFrontiers);
        }

        private static System.Threading.Timer PeriodicTimer;

        public static void StartPeriodicStats()
        {
            TimerCallback tcb = PeriodicTimerFunction;
            PeriodicTimer = new Timer(tcb, null, 300000, 300000);
        }

        
        public static void IncrementNumberOfSchedules()
        {
            Interlocked.Increment(ref NumOfSchedulesExplored);
        }

        public static void StopPeriodicStats()
        {
            PeriodicTimer.Dispose();
        }
    }
}