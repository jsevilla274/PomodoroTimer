using System;
using System.IO;
using System.Media;
using System.Text.RegularExpressions;
using System.Threading;

namespace PomodoroTimer
{
    class Program
    {
        static void Main(string[] args)
        {
            new PomodoroTimer();
        }
    }

    class PomodoroTimer
    {
        const int WORKTIME = 10000, RESTTIME = 8000, NOTIFYTIME = 15000;
        const string PAUSE = "pause", NEXT = "next", RESTART = "restart", HELP = "help", QUIT = "quit";
        string Command = "";
        bool WaitForAnyInput = false;
        ManualResetEvent TimerBlocker = new ManualResetEvent(false);
        SoundPlayer PeriodEndSound = new SoundPlayer(Path.Combine(Directory.GetCurrentDirectory(),
            "sounds", "lingeringbells.wav"));
        SoundPlayer NotificationSound = new SoundPlayer(Path.Combine(Directory.GetCurrentDirectory(),
            "sounds", "notify.wav"));

        public PomodoroTimer()
        {
            Thread userInThread = new Thread(UserIn);
            userInThread.Start();

            Thread timerThread = new Thread(Timer);
            timerThread.Start();

            userInThread.Join();
            timerThread.Join();
        }

        void Timer()
        {
            string periodLabel = "";
            int periodTime = 0;
            bool isWorkPeriod = true, wasPaused = false, wasRestarted = false;
            DateTime currentTime, periodEndTime;

            while (Command != QUIT)
            {
                currentTime = DateTime.Now;
                
                if (wasPaused)
                {
                    // do not reset label or time, use saved periodTime
                    wasPaused = false;
                    periodEndTime = currentTime.AddMilliseconds(periodTime);

                    Console.WriteLine("[{0}] Resumed: {1} | End: {2} ({3})", periodLabel,
                        currentTime.ToString("h:mm:ss tt"), periodEndTime.ToString("h:mm:ss tt"),
                        (periodEndTime - currentTime).ToString(@"mm\:ss"));
                }
                else if (wasRestarted)
                {
                    // do not reset label or time, use existing or modified periodTime
                    wasRestarted = false;
                    periodEndTime = currentTime.AddMilliseconds(periodTime);

                    Console.WriteLine("[{0}] Restarted: {1} | End: {2} ({3})", periodLabel,
                        currentTime.ToString("h:mm:ss tt"), periodEndTime.ToString("h:mm:ss tt"),
                        (periodEndTime - currentTime).ToString(@"mm\:ss"));
                }
                else // normal flow
                {
                    if (isWorkPeriod)
                    {
                        periodLabel = "WORK";
                        periodTime = WORKTIME;
                    }
                    else
                    {
                        periodLabel = "REST";
                        periodTime = RESTTIME;
                    }
                    periodEndTime = currentTime.AddMilliseconds(periodTime);

                    Console.WriteLine("[{0}] Start: {1} | End: {2} ({3})", periodLabel,
                        currentTime.ToString("h:mm:ss tt"), periodEndTime.ToString("h:mm:ss tt"),
                        (periodEndTime - currentTime).ToString(@"mm\:ss"));
                }

                // block thread for periodTime   
                if (TimerBlocker.WaitOne(periodTime))   // if signalled
                {
                    // manually signalled, reset blocker
                    TimerBlocker.Reset();

                    // handle commands
                    if (Command == PAUSE)
                    {
                        TimeSpan remainingPeriod = periodEndTime - DateTime.Now;
                        periodTime = (int)remainingPeriod.TotalMilliseconds;

                        Console.Write("Period paused, press enter to resume ");
                        WaitForAnyUserInput();

                        wasPaused = true;
                    }
                    else if (Command == NEXT)
                    {
                        isWorkPeriod = !isWorkPeriod;
                    }
                    else if (Command.Contains(RESTART))
                    {
                        wasRestarted = true;

                        // checking if "MM:SS" setting option present
                        MatchCollection numsInCommand = new Regex(@"\d+").Matches(Command);
                        if (numsInCommand.Count >= 2)
                        {
                            int minsTemp = Int32.Parse(numsInCommand[0].Value);
                            int secsTemp = Int32.Parse(numsInCommand[1].Value);

                            // converting to milliseconds
                            minsTemp *= 60000;
                            secsTemp *= 1000;

                            periodTime = minsTemp + secsTemp;
                        }
                        // else restart with normal period time
                    }
                    // else Command == "quit" -> do nothing, while condition will handle it
                    
                }
                else // if not signalled (timeout)
                {
                    isWorkPeriod = !isWorkPeriod;
                    PeriodEndSound.Play();

                    Console.Write("Period end, press enter to resume ");
                    WaitForAnyUserInput(true);
                }
            }

            // exiting while loop implies user is quitting
            Console.WriteLine("Quitting timer");
        }

        // blocks thread until user enters any input; note: assumes blocker is unset
        void WaitForAnyUserInput(bool notify = false)
        {
            WaitForAnyInput = true;
            if (notify)
            {
                while (!TimerBlocker.WaitOne(NOTIFYTIME))
                {
                    // play a notification sound every NOTIFYTIME seconds until signalled
                    NotificationSound.Play();
                }
            }
            else
            {
                TimerBlocker.WaitOne();
            }
            WaitForAnyInput = false;
            TimerBlocker.Reset();
        }

        void UserIn()
        {
            Console.WriteLine("Pomodoro Timer start, enter \"{0}\" to end the timer", QUIT);
            while (Command != QUIT)
            {
                Command = Console.ReadLine();
                if (Command == PAUSE || Command == NEXT || Command.Contains(RESTART) || WaitForAnyInput)
                {
                    // unblocks timer
                    TimerBlocker.Set();
                }
                else if (Command == HELP)
                {
                    commandInfo();
                }
            }

            // unblocks timer
            TimerBlocker.Set();
        }

        void commandInfo()
        {
            Console.WriteLine("\n[Commands]");
            Console.WriteLine(PAUSE + " - halts the current period while keeping the time");
            Console.WriteLine(NEXT + " - proceeds to the next period");
            Console.WriteLine(RESTART + " (MM:SS) - restarts the current period with the preset time or optionally with" +
                " a specific time formatted as \"MM:SS\"");
            Console.WriteLine(QUIT + " - ends the timer and exits the program");
            Console.WriteLine(HELP + " - displays this command reference\n");
        }
    }
}
