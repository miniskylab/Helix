using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using Helix.Core;
using Helix.Crawler.Abstractions;
using log4net;

namespace Helix.Crawler
{
    public class BrokenLinkCollector
    {
        readonly ILog _log;
        readonly StateMachine<CrawlerState, CrawlerCommand> _stateMachine;

        public IStatistics Statistics { get; private set; }

        public CrawlerState CrawlerState => _stateMachine.CurrentState;

        public BrokenLinkCollector()
        {
            _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            _stateMachine = new StateMachine<CrawlerState, CrawlerCommand>(PossibleTransitions(), CrawlerState.WaitingForInitialization);

            Dictionary<Transition<CrawlerState, CrawlerCommand>, CrawlerState> PossibleTransitions()
            {
                return new Dictionary<Transition<CrawlerState, CrawlerCommand>, CrawlerState>
                {
                    { Transition(CrawlerState.WaitingForInitialization, CrawlerCommand.Stop), CrawlerState.Completed },
                    { Transition(CrawlerState.WaitingForInitialization, CrawlerCommand.Initialize), CrawlerState.WaitingToRun },
                    { Transition(CrawlerState.WaitingToRun, CrawlerCommand.Run), CrawlerState.Running },
                    { Transition(CrawlerState.WaitingToRun, CrawlerCommand.Abort), CrawlerState.WaitingForStop },
                    { Transition(CrawlerState.WaitingForStop, CrawlerCommand.Stop), CrawlerState.Completed },
                    { Transition(CrawlerState.Running, CrawlerCommand.Stop), CrawlerState.Completed },
                    { Transition(CrawlerState.Running, CrawlerCommand.Pause), CrawlerState.Paused },
                    { Transition(CrawlerState.Completed, CrawlerCommand.MarkAsRanToCompletion), CrawlerState.RanToCompletion },
                    { Transition(CrawlerState.Completed, CrawlerCommand.MarkAsCancelled), CrawlerState.Cancelled },
                    { Transition(CrawlerState.Completed, CrawlerCommand.MarkAsFaulted), CrawlerState.Faulted },
                    { Transition(CrawlerState.Paused, CrawlerCommand.Resume), CrawlerState.Running }
                };
                Transition<CrawlerState, CrawlerCommand> Transition(CrawlerState fromState, CrawlerCommand command)
                {
                    return new Transition<CrawlerState, CrawlerCommand>(fromState, command);
                }
            }
        }

        static BrokenLinkCollector()
        {
            // TODO: A workaround for .Net Core 2.x bug. Should be removed in the future.
            AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", false);
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
        }
    }
}