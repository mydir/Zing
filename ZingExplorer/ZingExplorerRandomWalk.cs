﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;


namespace Microsoft.Zing
{
    /// <summary>
    /// This class performs naive random walk 
    /// </summary>

    public class ZingExplorerNaiveRandomWalk : ZingExplorer
    {
        /// <summary>
        /// Parallel worker threads for performing search.
        /// </summary>
        private Task[] searchWorkers;

        private int maxSearchDepth;
 
        public ZingExplorerNaiveRandomWalk() : base()
        {
            maxSearchDepth = 10000;
        }

        protected override ZingerResult IterativeSearchStateSpace()
        {
            //outer loop to search the state space iteratively
            do
            {
                //Increment the iterative bound
                ZingerConfiguration.zBoundedSearch.IncrementIterativeBound();

                try
                {
                    searchWorkers = new Task[ZingerConfiguration.DegreeOfParallelism];
                    //create parallel search threads
                    for(int i = 0; i < ZingerConfiguration.DegreeOfParallelism; i++)
                    {
                        searchWorkers[i] = Task.Factory.StartNew(SearchStateSpace, i);
                        System.Threading.Thread.Sleep(10);
                    }

                    //Wait for all search workers to finish
                    Task.WaitAll(searchWorkers);

                }
                catch(AggregateException ex)
                {
                    foreach(var inner in ex.InnerExceptions)
                    {
                        if((inner is ZingException))
                        {
                            return lastErrorFound;
                        }
                        else
                        {
                            ZingerUtilities.PrintErrorMessage("Unknown Exception in Zing:");
                            ZingerUtilities.PrintErrorMessage(inner.ToString());
                            return ZingerResult.ZingRuntimeError;
                        }
                    }
                }

                ZingerStats.NumOfFrontiers = -1;
                ZingerStats.PrintPeriodicStats();

            } 
            while (!ZingerConfiguration.zBoundedSearch.checkIfFinalCutOffReached());

            return ZingerResult.Success;
        }

        protected override void SearchStateSpace(object obj)
        {
            int myThreadId = (int)obj;
            int numberOfSchedulesExplored = 0;
            
            //frontier
            FrontierNode startfN = new FrontierNode(StartStateTraversalInfo);
            TraversalInfo startState = startfN.GetTraversalInfo(StartStateStateImpl, myThreadId);

            while(numberOfSchedulesExplored < ZingerConfiguration.MaxSchedulesPerIteration)
            {
                
                //increment the schedule count
                numberOfSchedulesExplored++;

                //random walk always starts from the start state ( no frontier ).
                TraversalInfo currentState = startState;

                while(currentState.CurrentDepth < maxSearchDepth)
                {
                    //kil the exploration if bug found
                    //Check if cancelation token triggered
                    if (CancelTokenZingExplorer.IsCancellationRequested)
                    {
                        //some task found bug and hence cancelling this task
                        return;
                    }

                    TraversalInfo nextSuccessor = currentState.GetNextSuccessorUniformRandomly();
                    ZingerStats.IncrementTransitionsCount();
                    ZingerStats.IncrementStatesCount();
                    if(nextSuccessor == null)
                    {
                        break;
                    }

                    TerminalState terminalState = nextSuccessor as TerminalState;
                    if(terminalState != null)
                    {
                        if (terminalState.IsErroneousTI)
                        {
                            lock (SafetyErrors)
                            {
                                // bugs found
                                SafetyErrors.Add(nextSuccessor.GenerateNonCompactTrace());
                                this.lastErrorFound = nextSuccessor.ErrorCode;
                            }

                            if (ZingerConfiguration.StopOnError)
                            {
                                CancelTokenZingExplorer.Cancel(true);
                                throw nextSuccessor.Exception;
                            }
                        }

                        break;
                    }

                    currentState = nextSuccessor;
                }

            }
        }

        protected override bool MustExplore(TraversalInfo ti)
        {
            throw new NotImplementedException();
        }

        protected override void VisitState(TraversalInfo ti)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// The explorer will perform uniform random walk over the delay bounded executions.
    /// </summary>
    public class ZingExplorerDelayBoundedRandomWalk : ZingExplorer
    {
        /// <summary>
        /// maximum length of the trace
        /// </summary>
        protected int maxScheduleLength;

        public ZingExplorerDelayBoundedRandomWalk() :base ()
        {
            maxScheduleLength = 10000;
        }
        
        /// <summary>
        /// Explores a deterministic schedule from the peek of the stack to the terminal state.
        /// </summary>
        protected void RunToCompletionWithDelayZero(Stack<TraversalInfo> searchStack)
        {
            var currentState = searchStack.Peek();

            while(currentState.CurrentDepth < maxScheduleLength)
            {

                TraversalInfo nextState = currentState.GetNextSuccessorUnderDelayZeroForRW();
                ZingerStats.IncrementTransitionsCount();
                ZingerStats.IncrementStatesCount();
                if(nextState == null)
                {
                    return;
                }

                TerminalState terminal = nextState as TerminalState;
                if(terminal != null)
                {
                    if(terminal.IsErroneousTI)
                    {
                        lock(SafetyErrors)
                        {
                            SafetyErrors.Add(nextState.GenerateNonCompactTrace());
                            this.lastErrorFound = nextState.ErrorCode;
                        }

                        if(ZingerConfiguration.StopOnError)
                        {
                            //Stop all tasks
                            CancelTokenZingExplorer.Cancel(true);
                            throw nextState.Exception;
                        }
                    }
                    return;
                }

                searchStack.Push(nextState);
                currentState = searchStack.Peek();   
            }
        }

        protected int RandomBackTrackAndDelay(Stack<TraversalInfo> searchStack,  int startPoint)
        {
            //back track the stack randomly to some point 
            var backtrackAt = ZingerUtilities.rand.Next(startPoint, searchStack.Count() + 1);
            while(searchStack.Count() > backtrackAt)
            {
                searchStack.Pop();
            }
            //try to get to an execution state
            while(searchStack.Count > 1 && !(searchStack.Peek() is ExecutionState))
            {
                searchStack.Pop();
            }
            //delay at
            var currentState = searchStack.Peek();
            //delay the schedule
            var delayedState = (currentState as ExecutionState).GetDelayedSuccessor();

            if (delayedState == null)
            {
                return -1;
            }

            TerminalState terminal = delayedState as TerminalState;
            if (terminal != null)
            {
                if (terminal.IsErroneousTI)
                {
                    lock (SafetyErrors)
                    {
                        SafetyErrors.Add(delayedState.GenerateNonCompactTrace());
                        this.lastErrorFound = delayedState.ErrorCode;
                    }

                    if (ZingerConfiguration.StopOnError)
                    {
                        //Stop all tasks
                        CancelTokenZingExplorer.Cancel(true);
                        throw delayedState.Exception;
                    }
                }
                return -1;
            }
            //push it on stack
            searchStack.Push(delayedState);

            return searchStack.Count();
        }
        

        /// <summary>
        /// Parallel worker threads for performing search.
        /// </summary>
        private Task[] searchWorkers;

        protected override ZingerResult IterativeSearchStateSpace()
        {
            //outer loop to search the state space iteratively
            do
            {
                //Increment the iterative bound
                ZingerConfiguration.zBoundedSearch.IncrementIterativeBound();

                try
                {
                    searchWorkers = new Task[ZingerConfiguration.DegreeOfParallelism];
                    //create parallel search threads
                    for (int i = 0; i < ZingerConfiguration.DegreeOfParallelism; i++)
                    {
                        searchWorkers[i] = Task.Factory.StartNew(SearchStateSpace, i);
                        System.Threading.Thread.Sleep(10);
                    }

                    //Wait for all search workers to finish
                    Task.WaitAll(searchWorkers);

                }
                catch (AggregateException ex)
                {
                    foreach (var inner in ex.InnerExceptions)
                    {
                        if ((inner is ZingException))
                        {
                            return lastErrorFound;
                        }
                        else
                        {
                            ZingerUtilities.PrintErrorMessage("Unknown Exception in Zing:");
                            ZingerUtilities.PrintErrorMessage(inner.ToString());
                            return ZingerResult.ZingRuntimeError;
                        }
                    }
                }

                ZingerStats.NumOfFrontiers = -1;
                ZingerStats.PrintPeriodicStats();

            }
            while (!ZingerConfiguration.zBoundedSearch.checkIfFinalCutOffReached());

            return ZingerResult.Success;
        }

        protected override void SearchStateSpace(object obj)
        {
            int myThreadId = (int)obj;
            int numberOfSchedulesExplored = 0;
            int delayBudget = 0;
            Stack<TraversalInfo> searchStack = new Stack<TraversalInfo>();
            //frontier
            FrontierNode startfN = new FrontierNode(StartStateTraversalInfo);
            TraversalInfo startState = startfN.GetTraversalInfo(StartStateStateImpl, myThreadId);

            while (numberOfSchedulesExplored < ZingerConfiguration.MaxSchedulesPerIteration)
            {
                //kil the exploration if bug found
                //Check if cancelation token triggered
                if (CancelTokenZingExplorer.IsCancellationRequested)
                {
                    //some task found bug and hence cancelling this task
                    return;
                }

                delayBudget = ZingerConfiguration.zBoundedSearch.IterativeCutoff;
                //increment the schedule count
                numberOfSchedulesExplored++;
                searchStack = new Stack<TraversalInfo>();
                searchStack.Push(startState);
                int lastStartPoint = 1;
                while(delayBudget > 0)
                {
                    RunToCompletionWithDelayZero(searchStack);
                    lastStartPoint = RandomBackTrackAndDelay(searchStack, lastStartPoint);
                    if(lastStartPoint == -1)
                    {
                        break;
                    }
                    delayBudget--;
                }
            }
        }
        protected override bool MustExplore(TraversalInfo ti)
        {
            throw new NotImplementedException();
        }

        protected override void VisitState(TraversalInfo ti)
        {
            throw new NotImplementedException();
        }
    }

    
}
