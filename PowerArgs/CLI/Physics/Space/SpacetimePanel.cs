using System.Reflection;

namespace PowerArgs.Cli.Physics;

public class SpaceTimePanel : ConsolePanel, ISpaceTimeUI
{
    private readonly Dictionary<SpacialElement, SpacialElementRenderer?> renderers;
    private readonly SpacialElementBinder thingBinder;

    public SpaceTimePanel(SpaceTime? st)
    {
        SpaceTime = st;
        Width = ConsoleMath.Round(st.Width);
        Height = ConsoleMath.Round(st.Height);
        Background = ConsoleColor.White;
        renderers = new Dictionary<SpacialElement, SpacialElementRenderer?>();
        thingBinder = new SpacialElementBinder();
        SubscribeForLifetime(this, nameof(Bounds), SizeChanged.Fire);
        new SpaceTimeUIHost(this);
    }

    public Event<SpacialElement> OnBind { get; } = new();
    public RectF CameraBounds => new(CameraTopLeft.Left, CameraTopLeft.Top, SpaceTime.Width, SpaceTime.Height);
    public SpaceTime? SpaceTime { get; }
    public RealTimeViewingFunction RealTimeViewing { get; set; }
    public Event SizeChanged { get; } = new();

    public Event AfterUpdate { get; } = new();

    float ISpaceTimeUI.Width => Width;

    float ISpaceTimeUI.Height => Height;

    public LocF CameraTopLeft { get; set; }

    public void Invoke(Action a) => Application.InvokeNextCycle(a);

    public void Add(SpacialElement element)
    {
        var renderer = thingBinder.Bind(element, SpaceTime);
        renderers.Add(element, renderer);
        Controls.Add(renderer);
        OnBind.Fire(element);
        renderer.OnRender();
    }

    public void Remove(SpacialElement element)
    {
        Controls.Remove(renderers[element]);
        renderers.Remove(element);
    }

    public void UpdateBounds(SpacialElement e, float xf, float yf, int z, float wf, float hf)
    {
        var x = ConsoleMath.Round(xf);
        var y = ConsoleMath.Round(yf);
        var w = ConsoleMath.Round(wf);
        var h = ConsoleMath.Round(hf);

        var r = renderers[e];
        if (x != r.X || y != r.Y || w != r.Width || h != r.Height)
        {
            r.Width = w;
            r.Height = h;
            #if DEBUG
            if (float.IsInfinity(x) || float.IsNaN(x) || (x >= -100000 == false && x < 100000 == false))
                throw new Exception("Out of bounds: " + x + "," + y);
            #endif

            r.X = x;
            r.Y = y;
        }

        r.OnRender();
    }
}

public class SpacialElementBindingAttribute : Attribute
{
    public SpacialElementBindingAttribute(Type t) { ThingType = t; }
    public Type ThingType { get; set; }
}

public class SpacialElementBinder
{
    private static readonly Dictionary<Type, Type> Bindings = LoadBindings();

    public SpacialElementRenderer? Bind(SpacialElement t, SpaceTime? spaceTime)
    {
        Type binding;
        if (Bindings.TryGetValue(t.GetType(), out binding) == false)
        {
            binding = typeof(SpacialElementRenderer);
        }

        var ret = Activator.CreateInstance(binding) as SpacialElementRenderer;
        t.Renderer = ret;
        ret.Element = t;
        ret.Spacetime = spaceTime;
        ret.OnBind();
        return ret;
    }

    private static Dictionary<Type, Type> LoadBindings()
    {
        var ret = new Dictionary<Type, Type>();
        var rendererTypes = new List<Type>();

        foreach (var rendererAssembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (rendererAssembly.IsDynamic)
            {
                continue;
            }

            var types = rendererAssembly.ExportedTypes.ToList();
            foreach (var t in from type in types where IsRendererType(type) select type)
            {
                if (t.GetTypeInfo().GetCustomAttributes(typeof(SpacialElementBindingAttribute), true).Count() !=
                    1) continue;

                rendererTypes.Add(t);
            }
        }

        foreach (var thingAssembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.IsDynamic == false))
        {
            foreach (var t in
                     from type in thingAssembly.ExportedTypes
                     where type.GetTypeInfo().IsSubclassOf(typeof(SpacialElement))
                     select type)
                ret.Add(t, FindMatchingBinder(rendererTypes, t));
        }

        return ret;
    }

    private static bool IsRendererType(Type type) => type.GetTypeInfo().IsSubclassOf(typeof(SpacialElementRenderer));

    private static Type FindMatchingBinder(List<Type> rendererTypes, Type thingType)
    {
        var match = (
            from renderer in rendererTypes
            where (renderer.GetTypeInfo().GetCustomAttributes(typeof(SpacialElementBindingAttribute), true).First() as
                    SpacialElementBindingAttribute).ThingType ==
                thingType
            select renderer).SingleOrDefault();

        if (match == null && thingType.GetTypeInfo().BaseType.GetTypeInfo().IsSubclassOf(typeof(SpacialElement)))
        {
            return FindMatchingBinder(rendererTypes, thingType.GetTypeInfo().BaseType);
        }

        if (match != null)
        {
            return match;
        }

        return typeof(SpacialElementRenderer);
    }
}

public class SpacialElementRenderer : ConsolePanel
{
    public SpacialElement Element { get; set; }
    public SpaceTime? Spacetime { get; set; }

    public virtual void OnBind() { CompositionMode = Element.CompositionMode; }

    public virtual void OnRender()
    {
        Background = Element.BackgroundColor;
        ZIndex = Element.ZIndex;
        CompositionMode = Element.CompositionMode;
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        if (Element.Pen.HasValue)
        {
            context.Fill(Element.Pen.Value);
        }
        else
        {
            base.OnPaint(context);
        }
    }
}