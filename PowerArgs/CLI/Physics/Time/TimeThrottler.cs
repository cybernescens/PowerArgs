using System;

namespace PowerArgs.Cli.Physics
{
    public class TimeThrottler : Lifetime
    {
        public int MaxIterationsPerTick { get; set; } = 1;
        private Action innerAction;
        private int iterationsThisTick;

        public TimeThrottler(Action innerAction, ILifetimeManager? lt)
        {
            this.innerAction = innerAction;
            Time.CurrentTime.EndOfCycle.SubscribeForLifetime(lt, () => iterationsThisTick = 0);
            lt.OnDisposed(this.Dispose);
        }

        public void Invoke()
        {
            if (IsExpired == false && iterationsThisTick < MaxIterationsPerTick)
            {
                iterationsThisTick++;
                innerAction.Invoke();
            }
        }
    }
}
