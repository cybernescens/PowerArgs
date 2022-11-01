using PowerArgs.Cli.Physics;

namespace PowerArgs.Cli;

/// <summary>
///     An attribute that tells the form generator to ignore this
///     property
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class FormIgnoreAttribute : Attribute { }

/// <summary>
///     An attribute that tells the form generator to give this
///     property a read only treatment
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class FormReadOnlyAttribute : Attribute { }

/// <summary>
///     An attribute that tells the form generator to use yes no labels for a toggle
/// </summary>
public class FormYesNoAttribute : Attribute { }

public class FormSliderAttribute : Attribute
{
    public RGB BarColor { get; set; } = RGB.White;
    public RGB HandleColor { get; set; } = RGB.Gray;
    public float Min { get; set; } = 0;
    public float Max { get; set; } = 100;
    public float Value { get; set; } = 0;
    public float Increment { get; set; } = 10;
    public bool EnableWAndSKeysForUpDown { get; set; } = false;

    public Slider Factory() =>
        new() {
            BarColor = BarColor,
            HandleColor = HandleColor,
            Min = Min,
            Max = Max,
            Value = Value,
            Increment = Increment,
            EnableWAndSKeysForUpDown = EnableWAndSKeysForUpDown
        };
}

/// <summary>
///     An attribute that tells the form generator to give this
///     property a specific value width
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class FormWidth : Attribute
{
    /// <summary>
    ///     Creates a new FormWidth attribute
    /// </summary>
    /// <param name="width"></param>
    public FormWidth(int width) { Width = width; }

    public int Width { get; }
}

/// <summary>
///     An attribute that lets you override the display string
///     on a form element
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Enum | AttributeTargets.Field)]
public class FormLabelAttribute : Attribute
{
    /// <summary>
    ///     Initialized the attribute
    /// </summary>
    /// <param name="label">The label to display on the form element</param>
    public FormLabelAttribute(string label) { Label = label; }

    /// <summary>
    ///     The label to display on the form element
    /// </summary>
    public string Label { get; set; }
}

/// <summary>
///     A class that represents a form element
/// </summary>
public class FormElement
{
    /// <summary>
    ///     The label for the form element
    /// </summary>
    public ConsoleString Label { get; init; } = null!;
    /// <summary>
    ///     The control that renders the form element's value
    /// </summary>
    public ConsoleControl ValueControl { get; init; } = null!;
}

/// <summary>
///     Options for configuring a form
/// </summary>
public class FormOptions
{
    /// <summary>
    ///     The percentage of the available width to use for labels
    /// </summary>
    public double LabelColumnPercentage { get; set; }

    /// <summary>
    ///     The form elements to render
    /// </summary>
    public ObservableCollection<FormElement> Elements { get; private set; } = new();

    /// <summary>
    ///     Autogenerates form options for the given object by reflecting on its properties. All public properties with getters
    ///     and setters will be included in the form unless it has the FormIgnore attribute on it. This method supports
    ///     strings,
    ///     ints, and enums.
    ///     The form will be configured to two way bind all the form elements to the property values.
    /// </summary>
    /// <param name="o">The object to create form options for</param>
    /// <param name="labelColumnPercentage">the label column percentage to use</param>
    /// <returns></returns>
    public static FormOptions FromObject(object o, double labelColumnPercentage = .25)
    {
        var properties = o.GetType().GetProperties().Where(
                p => p.HasAttr<FormIgnoreAttribute>() == false && p.GetSetMethod() != null && p.GetGetMethod() != null)
            .ToList();

        var ret = new FormOptions {
            Elements = new ObservableCollection<FormElement>(),
            LabelColumnPercentage = labelColumnPercentage
        };

        foreach (var property in properties)
        {
            if (o is IObservableObject && property.Name == nameof(IObservableObject.SuppressEqualChanges))
            {
                continue;
            }

            ConsoleControl? editControl = null;
            if (property.HasAttr<FormReadOnlyAttribute>() == false && property.PropertyType == typeof(string))
            {
                var value = (string)property.GetValue(o);
                var textBox = new TextBox
                    { Foreground = ConsoleColor.White, Value = value == null ? ConsoleString.Empty : value.ToWhite() };

                textBox.SynchronizeForLifetime(
                    nameof(textBox.Value),
                    () => property.SetValue(o, textBox.Value.ToString()),
                    textBox);

                (o as IObservableObject)?.SynchronizeForLifetime(
                    property.Name,
                    () => {
                        var valueRead = property.GetValue(o);
                        if (valueRead is ICanBeAConsoleString consoleString)
                        {
                            textBox.Value = consoleString.ToConsoleString();
                        }
                        else
                        {
                            textBox.Value = (valueRead + "").ToWhite();
                        }
                    },
                    textBox);

                editControl = textBox;
            }
            else if (property.HasAttr<FormReadOnlyAttribute>() == false && property.PropertyType == typeof(int))
            {
                if (property.HasAttr<FormSliderAttribute>())
                {
                    var value = (int)property.GetValue(o);
                    var slider = property.Attr<FormSliderAttribute>().Factory();
                    slider.Value = value;
                    slider.SynchronizeForLifetime(
                        nameof(slider.Value),
                        () => { property.SetValue(o, (int)slider.Value); },
                        slider);

                    (o as IObservableObject)?.SynchronizeForLifetime(
                        property.Name,
                        () => {
                            var valueRead = (int)property.GetValue(o);
                            slider.Value = valueRead;
                        },
                        slider);

                    editControl = slider;
                }
                else
                {
                    var value = (int)property.GetValue(o);
                    var textBox = new TextBox { Foreground = ConsoleColor.White, Value = value.ToString().ToWhite() };
                    textBox.SynchronizeForLifetime(
                        nameof(textBox.Value),
                        () => {
                            if (textBox.Value.Length == 0)
                            {
                                textBox.Value = "0".ToConsoleString();
                            }

                            if (textBox.Value.Length > 0 && int.TryParse(textBox.Value.ToString(), out var result))
                            {
                                property.SetValue(o, result);
                            }
                            else if (textBox.Value.Length > 0)
                            {
                                textBox.Value = property.GetValue(o).ToString().ToWhite();
                            }
                        },
                        textBox);

                    (o as IObservableObject)?.SynchronizeForLifetime(
                        property.Name,
                        () => {
                            var valueRead = property.GetValue(o);
                            if (valueRead is ICanBeAConsoleString)
                            {
                                textBox.Value = (valueRead as ICanBeAConsoleString).ToConsoleString();
                            }
                            else
                            {
                                textBox.Value = (valueRead + "").ToConsoleString();
                            }
                        },
                        textBox);

                    textBox.AddedToVisualTree.SubscribeForLifetime(
                        textBox,
                        () => {
                            var previouslyFocusedControl = textBox.Application.FocusManager.FocusedControl;

                            var emptyStringAction = new Action(
                                () => {
                                    if (previouslyFocusedControl == textBox &&
                                        textBox.Application.FocusManager.FocusedControl != textBox)
                                    {
                                        if (textBox.Value.Length == 0)
                                        {
                                            textBox.Value = "0".ToConsoleString();
                                            property.SetValue(o, 0);
                                        }
                                    }

                                    previouslyFocusedControl = textBox.Application.FocusManager.FocusedControl;
                                });

                            textBox.Application.FocusManager.SubscribeForLifetime(textBox, nameof(FocusManager.FocusedControl), emptyStringAction);
                        });

                    editControl = textBox;
                }
            }
            else if (property.HasAttr<FormReadOnlyAttribute>() == false && property.PropertyType.IsEnum)
            {
                var options = new List<DialogOption?>();
                foreach (var val in Enum.GetValues(property.PropertyType))
                {
                    var enumField = property.PropertyType.GetField(Enum.GetName(property.PropertyType, val));
                    var display = enumField.HasAttr<FormLabelAttribute>()
                        ? enumField.Attr<FormLabelAttribute>().Label.ToConsoleString()
                        : (val + "").ToConsoleString();

                    options.Add(new DialogOption(val.ToString(), val, display));
                }

                var dropdown = new Dropdown(options);
                dropdown.Width = Math.Min(40, options.Select(option => option.DisplayText.Length).Max() + 8);
                dropdown.SubscribeForLifetime(dropdown, nameof(dropdown.Value), () => property.SetValue(o, dropdown.Value.Value));

                (o as IObservableObject)?.SynchronizeForLifetime(
                    property.Name,
                    () => dropdown.Value = options.Where(option => option.Value.Equals(property.GetValue(o))).Single(),
                    dropdown);

                editControl = dropdown;
            }
            else if (property.HasAttr<FormReadOnlyAttribute>() == false && property.PropertyType == typeof(RGB))
            {
                var dropdown = new ColorPicker();
                dropdown.Width = Math.Min(
                    40,
                    Enums.GetEnumValues<ConsoleColor>().Select(option => option.ToString().Length).Max() + 8);

                dropdown.SubscribeForLifetime(dropdown, nameof(dropdown.Value), () => property.SetValue(o, dropdown.Value));

                (o as IObservableObject)?.SynchronizeForLifetime(
                    property.Name,
                    () => dropdown.Value = (RGB)property.GetValue(o),
                    dropdown);

                editControl = dropdown;
            }
            else if (property.HasAttr<FormReadOnlyAttribute>() == false && property.PropertyType == typeof(bool))
            {
                var toggle = new ToggleControl();

                if (property.HasAttr<FormYesNoAttribute>())
                {
                    toggle.OnLabel = " Yes ";
                    toggle.OffLabel = " No  ";
                }

                toggle.SubscribeForLifetime(toggle, nameof(toggle.On), () => property.SetValue(o, toggle.On));
                (o as IObservableObject)?.SynchronizeForLifetime(
                    property.Name,
                    () => toggle.On = (bool)property.GetValue(o),
                    toggle);

                editControl = toggle;
            }
            else
            {
                var value = property.GetValue(o);
                var valueString = value != null ? value.ToString().ToDarkGray() : "<null>".ToDarkGray();
                var valueLabel = new Label { CanFocus = true, Text = valueString + " (read only)".ToDarkGray() };
                (o as IObservableObject)?.SynchronizeForLifetime(
                    property.Name,
                    () => valueLabel.Text = (property.GetValue(o) + "").ToConsoleString() + " (read only)".ToDarkGray(),
                    valueLabel);

                editControl = valueLabel;
            }

            if (property.HasAttr<FormWidth>())
            {
                editControl.Width = property.Attr<FormWidth>().Width;
            }

            ret.Elements.Add(
                new FormElement {
                    Label = property.HasAttr<FormLabelAttribute>()
                        ? property.Attr<FormLabelAttribute>().Label.ToYellow()
                        : property.Name.ToYellow(),
                    ValueControl = editControl
                });
        }

        return ret;
    }
}

/// <summary>
///     A control that lets users edit a set of values as in a form
/// </summary>
public class Form : ConsolePanel
{
    /// <summary>
    ///     Creates a form using the given options
    /// </summary>
    /// <param name="options">form options</param>
    public Form(FormOptions options)
    {
        Options = options;
        AddedToVisualTree.SubscribeForLifetime(this, InitializeForm);
    }

    /// <summary>
    ///     The options that were provided
    /// </summary>
    public FormOptions Options { get; }

    private void InitializeForm()
    {
        var labelColumn = Add(new StackPanel { Orientation = Orientation.Vertical, Margin = 1 });
        var valueColumn = Add(new StackPanel { Orientation = Orientation.Vertical, Margin = 1 });

        SynchronizeForLifetime(
            nameof(Bounds),
            () => {
                var labelColumnWidth = ConsoleMath.Round(Width * Options.LabelColumnPercentage);
                var valueColumnWidth = ConsoleMath.Round(Width * (1 - Options.LabelColumnPercentage));

                while (labelColumnWidth + valueColumnWidth > Width)
                {
                    labelColumnWidth--;
                }

                while (labelColumnWidth + valueColumnWidth < Width)
                {
                    valueColumnWidth++;
                }

                labelColumn.Width = labelColumnWidth;
                valueColumn.Width = valueColumnWidth;

                labelColumn.Height = Height;
                valueColumn.Height = Height;

                valueColumn.X = labelColumnWidth;
            },
            this);

        foreach (var element in Options.Elements)
        {
            labelColumn.Add(new Label { Height = 1, Text = element.Label }).FillHorizontally();
            element.ValueControl.Height = 1;
            valueColumn.Add(element.ValueControl);
            EnsureSizing(element);
        }

        Options.Elements.Added.SubscribeForLifetime(
            this,
            addedElement => {
                var index = Options.Elements.IndexOf(addedElement);
                var label = new Label { Height = 1, Text = addedElement.Label };
                addedElement.ValueControl.Height = 1;
                labelColumn.Controls.Insert(index, label);
                label.FillHorizontally();
                valueColumn.Controls.Insert(index, addedElement.ValueControl);
                EnsureSizing(addedElement);
            });

        Options.Elements.Removed.SubscribeForLifetime(
            this,
            removedElement => {
                var index = valueColumn.Controls.IndexOf(removedElement.ValueControl);
                labelColumn.Controls.RemoveAt(index);
                valueColumn.Controls.RemoveAt(index);
            });

        Options.Elements.AssignedToIndex.SubscribeForLifetime(
            this,
            (object[] _) => throw new NotSupportedException("Index assignments not supported in form elements"));
    }

    private void EnsureSizing(FormElement? element)
    {
        if (element.ValueControl is ToggleControl == false && element.ValueControl is Dropdown == false)
        {
            if (element.ValueControl.Width == 0)
            {
                element.ValueControl.FillHorizontally();
            }
        }
    }
}