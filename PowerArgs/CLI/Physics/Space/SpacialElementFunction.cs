namespace PowerArgs.Cli.Physics
{
    public abstract class SpacialElementFunction : TimeFunction
    {
        public SpacialElement? Element { get; set; }
        public SpacialElementFunction(SpacialElement? target)
        {
            this.Element = target;

            if (target.Lifetime.IsExpired)
            {
                return;
            }

            if (target.IsAttached())
            {
                Time.CurrentTime.InvokeNextCycle(() => 
                {
                    if (target.Lifetime.IsExpired == false && target.IsAttached())
                    {
                        Time.CurrentTime.Add(this);
                    }
                 });
            }
            else
            {
                target.Added.SubscribeForLifetime(target.Lifetime, () => { Time.CurrentTime.Add(this); });
            }


            Element.Lifetime.OnDisposed(()=>
            {
                if (this.Lifetime.IsExpired == false)
                {
                    this.Lifetime.Dispose();
                }
            });
        }
    }
}
