using System;
using System.Linq;
namespace PowerArgs.Cli.Physics
{
    public static class NoOverlapEnforcer
    {
        public class OverlapInfo
        {
            public SpacialElement DetectingElement { get; set; }
            public ICollider OverlappingElement { get; set; }
        }

        public static void EnableForLifetime(Func<SpacialElement, bool> filter, Action<OverlapInfo> handler, ILifetimeManager? lt)
        {
            foreach (var element in SpaceTime.CurrentSpaceTime.Elements)
            {
                var myElement = element;
                myElement.SizeOrPositionChanged.SubscribeForLifetime(lt, () => AssertNoOverlaps(filter, myElement, handler));
            }

            SpaceTime.CurrentSpaceTime.SpacialElementAdded.SubscribeForLifetime(lt,
                (newEl) =>
                {
                    newEl.SizeOrPositionChanged.SubscribeForLifetime(lt, () => AssertNoOverlaps(filter, newEl, handler));
                });
        }

        private static ICollider GetObstacleIfMovedTo(SpacialElement el, int? z = null)
        {
            var overlaps = el.GetObstacles(z).Where(e => e.MassBounds.Touches(el.Bounds)).ToArray();
            return overlaps.FirstOrDefault();
        }

        private static void AssertNoOverlaps(Func<SpacialElement, bool> filter, SpacialElement el, Action<OverlapInfo> handler)
        {
            if (filter(el) == false) return;

            var overlappingObstacle = GetObstacleIfMovedTo(el);

            if (overlappingObstacle != null)
            {
                handler(new OverlapInfo() { DetectingElement = el, OverlappingElement = overlappingObstacle });
            }
        }
    }
}
