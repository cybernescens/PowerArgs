using System.Text.RegularExpressions;

namespace PowerArgs.Cli;

public class ConsoleBitmapAnimationStudio : ProtectedConsolePanel
{
    public static readonly RGB TimespanPopupBGColor = RGB.Gray;

    private bool pendingChanges;

    private readonly InMemoryConsoleBitmapVideo animation = new() {
        Frames = new List<InMemoryConsoleBitmapFrame>
            { new(TimeSpan.Zero, new ConsoleBitmap(40, 20)) }
    };

    private StackPanel buttonStack;
    private ConsolePanel commandBar;
    private int currentFrameIndex;
    private string? currentlyOpenedFile;
    private ConsoleBitmapEditor editor;
    private ListGrid<InMemoryConsoleBitmapFrame> frameList;
    private ConsolePanel framePanel;

    private string? lastOpenDialogInput;

    private GridLayout layout;
    private ConsolePanel previewPanel;
    private readonly Event refreshed = new();
    private bool saveInProgress;
    private UndoRedoStack undoRedo;

    public ConsoleBitmapAnimationStudio()
    {
        InitUndoRedoStack();
        InitLayout();
        InitFramePanel();
        InitPreviewPanel();
        InitCommandBar();
    }

    private InMemoryConsoleBitmapFrame CurrentFrame => animation.Frames[currentFrameIndex];
    private ConsoleBitmap CurrentBitmap => CurrentFrame.Bitmap;

    private void InitUndoRedoStack()
    {
        undoRedo = new UndoRedoStack();
        undoRedo.OnUndoRedoAction.SubscribeForLifetime(this, Refresh);
        undoRedo.OnUndoRedoAction.SubscribeForLifetime(
            this,
            () => {
                if (undoRedo.UndoElements.FirstOrDefault() is ClearPendingChangesAction == false)
                {
                    SetPendingChanges(true);
                }
            });

        undoRedo.OnEmptyUndoStack.SubscribeForLifetime(this, () => SetPendingChanges(false));
    }

    private void InitLayout()
    {
        layout = ProtectedPanel.Add(
            new GridLayout(
                new GridLayoutOptions {
                    Columns = new List<GridColumnDefinition> {
                        new() { Type = GridValueType.Pixels, Width = 15 },
                        new() { Type = GridValueType.RemainderValue, Width = 1 }
                    },
                    Rows = new List<GridRowDefinition> {
                        new() { Type = GridValueType.Pixels, Height = 3 },
                        new() { Type = GridValueType.RemainderValue, Height = 1 }
                    }
                })).Fill();

        layout.RefreshLayout();
        commandBar = layout.Add(new ConsolePanel(), 0, 0, 2);
        framePanel = layout.Add(new ConsolePanel(), 0, 1);
        previewPanel = layout.Add(new ConsolePanel(), 1, 1);
        layout.RefreshLayout();
    }

    private void InitFramePanel()
    {
        // vertical divider
        framePanel.Add(new ConsolePanel { Width = 1, Background = RGB.White }).DockToRight().FillVertically();

        var options = new ListGridOptions<InMemoryConsoleBitmapFrame> {
            DataSource = new SyncList<InMemoryConsoleBitmapFrame>(animation.Frames),
            Columns = new List<ListGridColumnDefinition<InMemoryConsoleBitmapFrame>> {
                new() {
                    Width = framePanel.Width - 2,
                    Type = GridValueType.Pixels,
                    Formatter = f => new Label { Text = $"Frame {animation.Frames.IndexOf(f) + 1}".ToWhite() },
                    Header = "Frame".ToYellow()
                }
            },
            ShowColumnHeaders = false,
            ShowPager = false,
            EnablePagerKeyboardShortcuts = false
        };

        frameList = framePanel.Add(new ListGrid<InMemoryConsoleBitmapFrame>(options))
            .Fill(padding: new Thickness(1, 1, 0, 0));

        refreshed.SubscribeForLifetime(
            frameList,
            () => { frameList.Refresh(); });

        BorderPanel? fakeDialog = null;
        Label? fakeDialogLabel = null;

        frameList.SelectionChanged.SubscribeForLifetime(
            frameList,
            () => {
                currentFrameIndex = frameList.SelectedRowIndex;
                Refresh();
                if (fakeDialog != null)
                {
                    fakeDialog.X = frameList.AbsoluteX + frameList.Width - 2;
                    fakeDialog.Y = frameList.Y + 1 + frameList.SelectedRowIndex;
                    fakeDialogLabel!.Text = FormatTimespanForFramePopup(CurrentFrame.FrameTime);
                }
            });

        frameList.Focused.SubscribeForLifetime(
            frameList,
            () => {
                var fakeDialogContent = new ConsolePanel { Background = TimespanPopupBGColor };

                fakeDialog = Application.LayoutRoot.Add(new BorderPanel(fakeDialogContent));
                fakeDialog.BorderColor = RGB.Blue;
                fakeDialog.Width = 30;
                fakeDialog.Height = 5;
                fakeDialog.X = frameList.AbsoluteX + frameList.Width - 2;
                fakeDialog.Y = frameList.Y + 1 + frameList.SelectedRowIndex;
                fakeDialogContent.Fill();

                fakeDialogLabel = fakeDialogContent
                    .Add(new Label { Text = FormatTimespanForFramePopup(CurrentFrame.FrameTime) }).CenterBoth();

                refreshed.SubscribeForLifetime(
                    fakeDialogLabel,
                    () => { fakeDialogLabel.Text = FormatTimespanForFramePopup(CurrentFrame.FrameTime); });

                Application.FocusManager.GlobalKeyHandlers.PushForLifetime(
                    ConsoleKey.OemPlus,
                    null,
                    () => { undoRedo.Do(new ShiftTimeStampForwardAction(this, currentFrameIndex, 50)); },
                    fakeDialog);

                Application.FocusManager.GlobalKeyHandlers.PushForLifetime(
                    ConsoleKey.OemMinus,
                    null,
                    () => { undoRedo.Do(new ShiftTimeStampBackwardAction(this, currentFrameIndex, 50)); },
                    fakeDialog);

                Application.FocusManager.GlobalKeyHandlers.PushForLifetime(
                    ConsoleKey.OemPlus,
                    ConsoleModifiers.Shift,
                    () => { undoRedo.Do(new ShiftTimeStampForwardAction(this, currentFrameIndex, 10)); },
                    fakeDialog);

                Application.FocusManager.GlobalKeyHandlers.PushForLifetime(
                    ConsoleKey.OemMinus,
                    ConsoleModifiers.Shift,
                    () => { undoRedo.Do(new ShiftTimeStampBackwardAction(this, currentFrameIndex, 10)); },
                    fakeDialog);
            });

        frameList.Unfocused.SubscribeForLifetime(
            frameList,
            () => {
                fakeDialog?.Dispose();
                fakeDialogLabel = null;
                fakeDialog = null;
            });
    }

    private void InitPreviewPanel()
    {
        previewPanel.Background = new RGB(130, 130, 130);
        editor = previewPanel.Add(new ConsoleBitmapEditor(CurrentBitmap)).CenterBoth();
        editor.BitmapChanged.SubscribeForLifetime(
            editor,
            action => { undoRedo.Done(action); });

        refreshed.SubscribeForLifetime(
            editor,
            () => { editor.UpdateBitmap(CurrentBitmap); });
    }

    private void InitCommandBar()
    {
        var divider = ConsoleString.Parse("[B=White] ");

        buttonStack = commandBar.Add(
                new StackPanel { X = 1, Margin = 1, Height = 1, Orientation = Orientation.Horizontal, AutoSize = true })
            .CenterVertically();

        var newCommand = buttonStack.Add(
            new Button { Text = "New".ToWhite(), Shortcut = new KeyboardShortcut(ConsoleKey.N, ConsoleModifiers.Alt) });

        newCommand.Pressed.SubscribeForLifetime(newCommand, NewCommandImpl);

        var openCommand = buttonStack.Add(
            new Button {
                Text = "Open".ToWhite(), Shortcut = new KeyboardShortcut(ConsoleKey.O, ConsoleModifiers.Alt)
            });

        openCommand.Pressed.SubscribeForLifetime(openCommand, OpenCommandImpl);

        var saveCommand = buttonStack.Add(
            new Button {
                Text = "Save".ToWhite(), Shortcut = new KeyboardShortcut(ConsoleKey.S, ConsoleModifiers.Alt)
            });

        saveCommand.Pressed.SubscribeForLifetime(saveCommand, SaveCommandImpl);

        var saveAsCommand = buttonStack.Add(new Button { Text = "Save as".ToWhite() });
        saveAsCommand.Pressed.SubscribeForLifetime(saveAsCommand, SaveAsCommandImpl);

        var undoCommand = buttonStack.Add(
            new Button {
                Text = "Undo".ToWhite(), Shortcut = new KeyboardShortcut(ConsoleKey.Z, ConsoleModifiers.Alt)
            });

        undoCommand.Pressed.SubscribeForLifetime(undoCommand, UndoCommandImpl);

        var redoCommand = buttonStack.Add(
            new Button {
                Text = "Redo".ToWhite(), Shortcut = new KeyboardShortcut(ConsoleKey.Y, ConsoleModifiers.Alt)
            });

        redoCommand.Pressed.SubscribeForLifetime(redoCommand, RedoCommandImpl);

        buttonStack.Add(new Label { Text = divider });

        var frameUpCommand = buttonStack.Add(
            new Button { Text = "Previous Frame".ToWhite(), Shortcut = new KeyboardShortcut(ConsoleKey.PageUp) });

        frameUpCommand.Pressed.SubscribeForLifetime(frameUpCommand, FrameUpCommandImpl);

        var frameDownCommand = buttonStack.Add(
            new Button { Text = "Next Frame".ToWhite(), Shortcut = new KeyboardShortcut(ConsoleKey.PageDown) });

        frameDownCommand.Pressed.SubscribeForLifetime(frameDownCommand, FrameDownCommandImpl);

        var duplicateCommand = buttonStack.Add(
            new Button {
                Text = "Duplicate Frame".ToWhite(), Shortcut = new KeyboardShortcut(ConsoleKey.D, ConsoleModifiers.Alt)
            });

        duplicateCommand.Pressed.SubscribeForLifetime(frameUpCommand, DuplicateFrameCommandImpl);

        buttonStack.Add(new Label { Text = divider });

        foreach (var editorButton in editor.CreateStandardButtons())
            buttonStack.Add(editorButton);
    }

    private async void NewCommandImpl()
    {
        if (await ConfirmOkToProceedWithPendingChanges("Are you sure you want to start a new project?") ==
            false) return;

        var w = await Dialog.ShowRichTextInput(new RichTextDialogOptions(Application) { Message = "Width".ToYellow() });
        if (w == null) return;

        if (string.IsNullOrWhiteSpace(w?.StringValue) || int.TryParse(w.StringValue, out var width) == false)
        {
            await Dialog.ShowMessage(Application, $"Invalid width: '{w.StringValue}'");
            return;
        }

        var h = await Dialog.ShowRichTextInput(new RichTextDialogOptions(Application) { Message = "Height".ToYellow() });
        if (h == null) return;

        if (string.IsNullOrWhiteSpace(h?.StringValue) || int.TryParse(h.StringValue, out var height) == false)
        {
            await Dialog.ShowMessage(Application, $"Invalid height: '{h.StringValue}'");
            return;
        }

        currentlyOpenedFile = null;
        animation.Frames.Clear();
        animation.Frames.Add(new InMemoryConsoleBitmapFrame(TimeSpan.Zero, new ConsoleBitmap(width, height)));

        currentFrameIndex = 0;
        undoRedo.Clear();
        undoRedo.Do(new ClearPendingChangesAction(this));
        Refresh();
    }

    private void SaveCommandImpl()
    {
        if (currentlyOpenedFile == null)
        {
            SaveAsCommandImpl();
        }
        else
        {
            SaveCommon(currentlyOpenedFile);
        }
    }

    private async void SaveAsCommandImpl()
    {
        var input = await Dialog.ShowRichTextInput(
            new RichTextDialogOptions(Application) { Message = "Select destination".ToYellow() });

        if (input == null) return;

        SaveCommon(input.StringValue);
    }

    private async void OpenCommandImpl()
    {
        if (await ConfirmOkToProceedWithPendingChanges("Are you sure you want to open another project?") ==
            false) return;

        var input = await Dialog.ShowRichTextInput(
            new RichTextDialogOptions(Application) {
                DefaultValue = lastOpenDialogInput,
                Message = "Enter file path".ToYellow()
            });

        lastOpenDialogInput = input.StringValue;

        if (File.Exists(lastOpenDialogInput) == false)
        {
            await Dialog.ShowMessage(Application, $"File not found: {lastOpenDialogInput}".ToRed());
            OpenCommandImpl();
            return;
        }

        if (TryOpenConsoleBitmapReaderFormat(lastOpenDialogInput) == false &&
            TryOpenVisualFormat(lastOpenDialogInput) == false)
        {
            await Dialog.ShowMessage(Application, $"Failed to load file: {lastOpenDialogInput}".ToRed());
        }
        else
        {
            currentlyOpenedFile = lastOpenDialogInput;
            Refresh();
        }
    }

    private void UndoCommandImpl() { undoRedo.Undo(); }

    private void RedoCommandImpl() { undoRedo.Redo(); }

    private void FrameUpCommandImpl()
    {
        frameList.SelectedRowIndex = frameList.SelectedRowIndex == 0
            ? frameList.SelectedRowIndex
            : frameList.SelectedRowIndex - 1;
    }

    private void FrameDownCommandImpl()
    {
        frameList.SelectedRowIndex = frameList.SelectedRowIndex == animation.Frames.Count - 1
            ? frameList.SelectedRowIndex
            : frameList.SelectedRowIndex + 1;
    }

    private void DuplicateFrameCommandImpl() { undoRedo.Do(new DuplicateFrameAction(this)); }

    private async void SaveCommon(string? destination)
    {
        if (saveInProgress) return;

        try
        {
            saveInProgress = true;
            ConsoleBitmapVideoWriter writer = null;
            var tempFile = Path.GetTempFileName();
            var tempWriteSuccess = false;
            try
            {
                writer = new ConsoleBitmapVideoWriter(s => File.WriteAllText(tempFile, s));
                foreach (var frame in animation.Frames)
                    writer.WriteFrame(frame.Bitmap, desiredFrameTime: frame.FrameTime);

                tempWriteSuccess = true;
            }
            catch (Exception)
            {
                await Dialog.ShowMessage(Application, $"Failed to save file to {destination}".ToRed());
                return;
            }
            finally
            {
                writer?.Finish();
                if (tempWriteSuccess == false)
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch (Exception) { }
                }
            }

            try
            {
                // If we can't read this back then don't finalize the save. 
                // This ensures that if we wrote garbage (which should never happen)
                // then we won't corrupt whatever was on disk.
                using (var testStream = File.OpenRead(tempFile))
                {
                    var testReader = new ConsoleBitmapStreamReader(testStream);
                    testReader.ReadToEnd();
                }
            }
            catch (Exception)
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch (Exception) { }

                await Dialog.ShowMessage(Application, $"Failed to save file to {destination}".ToRed());
                return;
            }

            File.Delete(destination);
            File.Move(tempFile, destination);
            currentlyOpenedFile = destination;
            undoRedo.Do(new ClearPendingChangesAction(this));
        }
        finally
        {
            saveInProgress = false;
        }
    }

    private bool TryOpenConsoleBitmapReaderFormat(string? filePath)
    {
        try
        {
            using (var stream = File.OpenRead(filePath))
            {
                var reader = new ConsoleBitmapStreamReader(stream);
                var animation = reader.ReadToEnd();
                this.animation.Frames.Clear();
                this.animation.Frames.AddRange(animation.Frames);
                currentFrameIndex = 0;
                return true;
            }
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    private bool TryOpenVisualFormat(string? filePath)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);

            var currentFrame = "";
            var foundStartOfFrame = false;
            var isParsingColorPallateNow = false;
            var fullHashLineRegex = "^#+$";
            var animation = new InMemoryConsoleBitmapVideo();

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // expect new bitmap
                if (foundStartOfFrame == false)
                {
                    // expect hashes to start bitmap
                    if (Regex.IsMatch(line, fullHashLineRegex) == false) return false;

                    foundStartOfFrame = true;
                    currentFrame += line + "\n";
                }
                // parsing optional color pallate info after closing hashes
                else if (isParsingColorPallateNow)
                {
                    // check for start of next frame and send loop back by one if true
                    if (Regex.IsMatch(line, fullHashLineRegex))
                    {
                        var parsed = ConsoleBitmapVisualSerializer.Deserialize(currentFrame);
                        animation.Frames.Add(new InMemoryConsoleBitmapFrame(TimeSpan.FromSeconds(animation.Frames.Count), parsed));

                        currentFrame = "";
                        isParsingColorPallateNow = false;
                        foundStartOfFrame = false;
                        i--;
                    }
                    // else consider this a pallate line
                    else
                    {
                        currentFrame += line + "\n";
                    }
                }
                // detect end of bitmap
                else if (Regex.IsMatch(line, fullHashLineRegex))
                {
                    currentFrame += line + "\n";
                    isParsingColorPallateNow = true;
                }
                // continue bitmap
                else
                {
                    currentFrame += line + "\n";
                }
            }

            // grab the final frame
            if (string.IsNullOrWhiteSpace(currentFrame) == false)
            {
                var parsed = ConsoleBitmapVisualSerializer.Deserialize(currentFrame);
                animation.Frames.Add(new InMemoryConsoleBitmapFrame(TimeSpan.FromSeconds(animation.Frames.Count), parsed));
            }

            this.animation.Frames.Clear();
            this.animation.Frames.AddRange(animation.Frames);
            currentFrameIndex = 0;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void Refresh()
    {
        refreshed.Fire();
        Paint();
    }

    private ConsoleString FormatTimespanForFramePopup(TimeSpan timestamp) =>
        (CurrentFrame.FrameTime.TotalMilliseconds + " ms").ToBlack(TimespanPopupBGColor);

    private async Task<bool> ConfirmOkToProceedWithPendingChanges(string msg)
    {
        if (pendingChanges)
        {
            try
            {
                await Dialog.ShowYesConfirmation(Application, $"You have unsaved changes. {msg}".ToYellow());
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        return true;
    }

    private void SetPendingChanges(bool val) { pendingChanges = val; }

    [TransientUndoRedoAction]
    private class ClearPendingChangesAction : IUndoRedoAction
    {
        private readonly ConsoleBitmapAnimationStudio studio;
        private readonly bool werePendingChanges;

        public ClearPendingChangesAction(ConsoleBitmapAnimationStudio studio)
        {
            werePendingChanges = studio.pendingChanges;
            this.studio = studio;
        }

        public void Do() { studio.SetPendingChanges(false); }

        public void Redo() { studio.SetPendingChanges(false); }

        public void Undo() { studio.SetPendingChanges(werePendingChanges); }
    }

    private class ShiftTimeStampBackwardAction : IUndoRedoAction
    {
        private readonly float amount;
        private readonly int frameIndex;
        private bool shifted;
        private readonly ConsoleBitmapAnimationStudio studio;

        public ShiftTimeStampBackwardAction(ConsoleBitmapAnimationStudio studio, int frameIndex, float amount)
        {
            this.studio = studio;
            this.frameIndex = frameIndex;
            this.amount = amount;
        }

        public void Do()
        {
            var minTime = frameIndex == 0 ? TimeSpan.Zero : studio.animation.Frames[frameIndex - 1].FrameTime;
            var proposedTime = studio.animation.Frames[frameIndex].FrameTime.Add(TimeSpan.FromMilliseconds(-amount));
            if (proposedTime > minTime)
            {
                shifted = true;
                for (var i = frameIndex; i < studio.animation.Frames.Count; i++)
                    studio.animation.Frames[i].Add(TimeSpan.FromMilliseconds(-amount));
            }
            else
            {
                shifted = false;
            }
        }

        public void Redo() { Do(); }

        public void Undo()
        {
            if (shifted)
            {
                for (var i = frameIndex; i < studio.animation.Frames.Count; i++)
                    studio.animation.Frames[i].FrameTime.Add(TimeSpan.FromMilliseconds(amount));
            }
        }
    }

    private class ShiftTimeStampForwardAction : IUndoRedoAction
    {
        private readonly float amount;
        private readonly int frameIndex;
        private readonly ConsoleBitmapAnimationStudio studio;

        public ShiftTimeStampForwardAction(ConsoleBitmapAnimationStudio studio, int frameIndex, float amount)
        {
            this.studio = studio;
            this.frameIndex = frameIndex;
            this.amount = amount;
        }

        public void Do()
        {
            for (var i = frameIndex; i < studio.animation.Frames.Count; i++)
                studio.animation.Frames[i].FrameTime.Add(TimeSpan.FromMilliseconds(amount));
        }

        public void Redo() { Do(); }

        public void Undo()
        {
            for (var i = frameIndex; i < studio.animation.Frames.Count; i++)
                studio.animation.Frames[i].FrameTime.Add(TimeSpan.FromMilliseconds(-amount));
        }
    }

    private class DuplicateFrameAction : IUndoRedoAction
    {
        private ConsoleBitmap myBitmap;
        private readonly int myIndex;
        private TimeSpan myTime;
        private readonly ConsoleBitmapAnimationStudio studio;

        public DuplicateFrameAction(ConsoleBitmapAnimationStudio studio)
        {
            myIndex = studio.currentFrameIndex;
            this.studio = studio;
        }

        public void Do()
        {
            myTime = studio.CurrentFrame.FrameTime;
            myBitmap = studio.CurrentBitmap.Clone();
            studio.animation.Frames.Insert(
                myIndex,
                new InMemoryConsoleBitmapFrame(myTime, myBitmap));

            studio.Refresh();
            studio.currentFrameIndex = myIndex + 1;
            studio.frameList.SelectedRowIndex = myIndex + 1;
        }

        public void Redo()
        {
            studio.animation.Frames.Insert(
                myIndex,
                new InMemoryConsoleBitmapFrame(myTime, myBitmap));
        }

        public void Undo()
        {
            studio.animation.Frames.RemoveAt(myIndex);
            studio.currentFrameIndex = myIndex;
            studio.frameList.SelectedRowIndex = myIndex;
        }
    }
}