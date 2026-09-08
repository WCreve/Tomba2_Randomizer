using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Tomba2_Randomizer;

public partial class MainWindow : Window
{
    private Randomizer randomizer;

    private List<ItemDto> itemDtos;
    private List<AreaDto> areaDtos;
    private List<EventDto> eventDtos;
    private List<TeleportArea> teleportAreas;

    private List<ItemDto> itemDtosGUI;

    private MemoryManipulator memory;

    private Inventory inventory;
    private byte[] eventStatuses;
    private byte[] progressValues;

    DispatcherTimer updateTabTimer;
    DispatcherTimer findGameTimer;

    private Button selectedButton;

    public MainWindow()
    {
        InitializeComponent();

        itemDtos = new List<ItemDto>();
        areaDtos = new List<AreaDto>();
        eventDtos = new List<EventDto>();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());

        var assembly = Assembly.GetExecutingAssembly();

        using (StreamReader sr = new StreamReader(assembly.GetManifestResourceStream("Tomba2_Randomizer.JSON.items.json")))
        {
            string json = sr.ReadToEnd();
            itemDtos = JsonSerializer.Deserialize<List<ItemDto>>(json, options);
        }

        using (StreamReader sr = new StreamReader(assembly.GetManifestResourceStream("Tomba2_Randomizer.JSON.areas.json")))
        {
            string json = sr.ReadToEnd();
            areaDtos = JsonSerializer.Deserialize<List<AreaDto>>(json, options);
        }

        using (StreamReader sr = new StreamReader(assembly.GetManifestResourceStream("Tomba2_Randomizer.JSON.events.json")))
        {
            string json = sr.ReadToEnd();
            eventDtos = JsonSerializer.Deserialize<List<EventDto>>(json, options);
        }

        using (StreamReader sr = new StreamReader(assembly.GetManifestResourceStream("Tomba2_Randomizer.JSON.teleports.json")))
        {
            string json = sr.ReadToEnd();
            teleportAreas = JsonSerializer.Deserialize<List<TeleportArea>>(json, options);
        }

        updateTabTimer = new DispatcherTimer();
        updateTabTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
        updateTabTimer.Start();

        findGameTimer = new DispatcherTimer();
        findGameTimer.Interval = new TimeSpan(0, 0, 0, 0, 200);
        findGameTimer.Tick += FindGame;

        itemDtosGUI = itemDtos.GroupBy(i => i.Address).Select(i => i.First()).Union(itemDtos.Where(i => string.IsNullOrEmpty(i.DisplayName))).ToList();

        CmbAreas.ItemsSource = teleportAreas;
        CmbAreas.SelectedIndex = 0;
        CmbSections.SelectedIndex = 0;
        CmbItems.ItemsSource = itemDtosGUI.OrderBy(i => i.GUIName).ToList();

        InitializeEvents();
        InitializeProgressGrid();

        findGameTimer.Start();
    }

    private void FindGame(object? sender, EventArgs e)
    {
        var procs = Process.GetProcessesByName("Tomba2");

        if (procs.Length > 1)
        {
            LblHooked.Content = "Multiple instances of Tomba 2 found";
            LblHooked.Foreground = new SolidColorBrush(Colors.Red);
        }
        else if (procs.Length == 0)
        {
            LblHooked.Content = "No instance of Tomba 2 found";
            LblHooked.Foreground = new SolidColorBrush(Colors.Red);
        }
        else
        {
            try
            {
                memory = new MemoryManipulator(procs[0]);
            }
            catch (Exception ex)
            {
                // ignored
            }

            if (memory.IsGameRunning())
            {
                LblHooked.Content = "Connected";
                LblHooked.Foreground = new SolidColorBrush(Colors.Green);

                procs[0].EnableRaisingEvents = true;
                procs[0].Disposed += ProcessLost;
                procs[0].Exited += ProcessLost;

                TabInventory.IsEnabled = true;
                TabEvents.IsEnabled = true;
                TabTeleport.IsEnabled = true;
                TabProgress.IsEnabled = true;

                findGameTimer.Tick -= FindGame;
                findGameTimer.Tick += CheckStillRunning;

                if (randomizer != null) memory.SetupRandomizer(randomizer);
            }
            else
            {
                LblHooked.Content = "Process found. Start the game";
                LblHooked.Foreground = new SolidColorBrush(Colors.Orange);
            }
        }
    }

    private void CheckStillRunning(object? sender, EventArgs e)
    {
        if (memory == null || !memory.IsGameRunning())
        {
            findGameTimer.Tick -= CheckStillRunning;
            findGameTimer.Tick += FindGame;

            TabMain.IsSelected = true;
            TabInventory.IsEnabled = false;
            TabEvents.IsEnabled = false;
            TabTeleport.IsEnabled = false;
            TabProgress.IsEnabled = false;
            if (memory != null) memory.ProcessIsActive = false;
        }
    }

    #region Inventory

    private void TrackInventory(object? sender, EventArgs e)
    {
        var newInventory = memory.ReadInventory();
        if (!newInventory.Equals(inventory))
        {
            CnvItems.Children.Clear();

            var greenItems = itemDtosGUI.Where(i => i.Color == "Green" && newInventory.Counts.Where(c => c.Value > 0).Select(c => c.Address).Contains(Convert.ToInt32(i.Address, 16))).ToList();
            var otherItems = itemDtosGUI.Where(i => i.Color != "Green" && newInventory.Counts.Where(c => c.Value > 0).Select(c => c.Address).Contains(Convert.ToInt32(i.Address, 16))).ToList();

            foreach (var item in greenItems.Union(otherItems))
            {
                var btn = new Button
                {
                    Content = item.GUIName,
                    Width = 450,
                    Height = 60,
                    Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    FontSize = 28,
                    Tag = item
                };

                btn.Foreground = item.Color switch
                {
                    "Green" => new SolidColorBrush(Color.FromArgb(255, 115, 214, 107)),
                    "Blue" => new SolidColorBrush(Color.FromArgb(255, 90, 189, 222)),
                    "Pink" => new SolidColorBrush(Color.FromArgb(255, 222, 99, 148)),
                    _ => btn.Foreground
                };

                var position = newInventory.Positions.First(p => p.Address == Convert.ToInt32(item.Address, 16) + 256).Value;

                var topMargin = item.Color == "Green"
                    ? 60 * (position / 2)
                    : 60 * (position / 2 + (greenItems.Count + 1) / 2);
                var leftMargin = 450 * (position % 2) + 10;

                btn.Margin = new Thickness(leftMargin, topMargin, 0, 0);
                btn.Padding = new Thickness(5, 0, 0, 0);

                btn.Classes.Add("item");
                btn.Classes.Add($"{item.Color.ToLower()}");

                btn.PointerEntered += OnMouseEnterButton;
                btn.PointerExited += OnMouseLeaveButton;
                btn.Click += OnClickButton;

                CnvItems.Children.Add(btn);
            }

            CnvItems.Height = (greenItems.Count / 2 + 1) * 60 + (otherItems.Count / 2 + 1) * 60;

            inventory = newInventory;
        }
    }

    private async void BtnSelectedItemApply_OnClick(object? sender, RoutedEventArgs e)
    {
        int input;

        try
        {
            input = Convert.ToInt32(TxtSelectedItemAmount.Text);
        }
        catch (FormatException)
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Error", "Invalid Input");

            await box.ShowAsync();
            return;
        }

        if (input > 255)
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Error", "Max amount is 255");

            await box.ShowAsync();
        }
        else
        {
            if (CmbItems.SelectedItem != null)
            {
                var item = (ItemDto)CmbItems.SelectedItem;

                var currentItemCount = memory.ReadMemory(0xfab4 + item.InternalId);

                if (currentItemCount > input) memory.RemoveItemWithMessage(item.InternalId, (byte)(currentItemCount - input));
                else if (currentItemCount < input) memory.AddItemWithMessage(item.InternalId, (byte)(input - currentItemCount));
            }
        }
    }

    private void BtnGiveAll_Click(object? sender, RoutedEventArgs e)
    {
        byte topIndex = 0, bottomIndex = 0;

        foreach (var item in itemDtosGUI)
        {
            memory.WriteMemory(Convert.ToInt32(item.Address, 16), 5, true);
            memory.WriteMemory(Convert.ToInt32(item.Address, 16) + 256, item.Color == "Green" ? topIndex++ : bottomIndex++);
        }

        memory.WriteInventoryTopBottomAmount(true, topIndex);
        memory.WriteInventoryTopBottomAmount(false, bottomIndex);
    }

    private void OnClickButton(object? sender, EventArgs e)
    {
        if (sender == null) return;

        selectedButton = (Button)sender;

        if (selectedButton.Tag == null) return;

        var item = (ItemDto)selectedButton.Tag;

        CmbItems.SelectedItem = item;

        TxtSelectedItemAmount.Text = inventory.Counts.First(c => c.Address == Convert.ToInt32(item.Address, 16)).Value.ToString();
    }

    private void OnMouseEnterButton(object? sender, EventArgs e)
    {
        if (sender != null) ((Button)sender).Background = new SolidColorBrush(Color.FromArgb(255, 45, 45, 45));
    }

    private void OnMouseLeaveButton(object? sender, EventArgs e)
    {
        if (sender != null) ((Button)sender).Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
    }

    #endregion

    #region Events

    private void TrackEvents(object? sender, EventArgs e)
    {
        var newEventStatuses = memory.ReadEvents();

        if (!newEventStatuses.SequenceEqual(eventStatuses))
        {
            for (int i = 0; i < newEventStatuses.Length; i++)
            {
                if (eventStatuses[i] != newEventStatuses[i])
                {
                    var spl = CnvEvents.Children.OfType<StackPanel>().OrderBy(spl => spl.Tag).ToList()[i];
                    var lbl = spl.Children.OfType<Label>().First();

                    lbl.Foreground = newEventStatuses[i] switch
                    {
                        0 => new SolidColorBrush(Color.FromArgb(255, 255, 230, 132)),
                        1 => new SolidColorBrush(Color.FromArgb(255, 222, 99, 148)),
                        _ => new SolidColorBrush(Color.FromArgb(255, 132, 132, 134))
                    };
                }
            }

            eventStatuses = newEventStatuses;
        }
    }

    private void OnClickNotStartedButton(object? sender, EventArgs e)
    {
        if (sender == null) return;

        selectedButton = (Button)sender;

        if (selectedButton.Tag == null) return;

        var address = (string)selectedButton.Tag;

        memory.WriteMemory(Convert.ToInt32(address, 16), 0);
    }

    private void OnClickStartedButton(object? sender, EventArgs e)
    {
        if (sender == null) return;

        selectedButton = (Button)sender;

        if (selectedButton.Tag == null) return;

        var address = (string)selectedButton.Tag;

        memory.WriteMemory(Convert.ToInt32(address, 16), 1);
    }

    private void OnClickCompletedButton(object? sender, EventArgs e)
    {
        if (sender == null) return;

        selectedButton = (Button)sender;

        if (selectedButton.Tag == null) return;

        var address = (string)selectedButton.Tag;

        memory.WriteMemory(Convert.ToInt32(address, 16), 255);
    }

    #endregion

    #region Teleport

    private void CmbAreas_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CmbAreas.SelectedItem == null) return;

        var teleportArea = (TeleportArea)CmbAreas.SelectedItem;

        CmbSections.ItemsSource = teleportArea.Sections;
        CmbSections.SelectedIndex = 0;
    }

    private void BtnTeleport_OnClick(object? sender, RoutedEventArgs e)
    {
        if (CmbAreas.SelectedItem == null || CmbSections.SelectedItem == null) return;

        memory.Teleport((byte)((TeleportArea)CmbAreas.SelectedItem).Id, (byte)((Section)CmbSections.SelectedItem).Id);
    }

    private void CmbItems_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender == null) return;

        var combobox = (ComboBox)sender;

        if (combobox.SelectedItem == null) return;

        var item = (ItemDto)combobox.SelectedItem;

        TxtSelectedItemAmount.Text = inventory.Counts.First(c => c.Address == Convert.ToInt32(item.Address, 16)).Value.ToString();
    }

    #endregion

    #region Progress

    private void TrackProgress(object? sender, EventArgs e)
    {
        var newProgress = memory.ReadProgress();

        if (!newProgress.SequenceEqual(progressValues))
        {
            var index = 0;

            foreach (var spl in GrdProgress.Children.OfType<StackPanel>())
            {
                spl.Children.OfType<TextBox>().First().Text = Convert.ToString(newProgress[index++]);
            }

            progressValues = newProgress;
        }
    }

    private void BtnProgressRefresh_OnClick(object? sender, RoutedEventArgs e)
    {
        progressValues = new byte[168];
    }

    private async void BtnProgressSetValues_OnClick(object? sender, RoutedEventArgs e)
    {
        var bytes = new List<byte>();
        foreach (var spl in GrdProgress.Children.OfType<StackPanel>())
        {
            try
            {
                var input = Convert.ToByte(spl.Children.OfType<TextBox>().First().Text);
                bytes.Add(input);
            }
            catch (FormatException)
            {
                var box = MessageBoxManager
                    .GetMessageBoxStandard("Error", "Invalid input");

                await box.ShowAsync();
                return;
            }
            catch (OverflowException)
            {
                var box = MessageBoxManager
                    .GetMessageBoxStandard("Error", "Max amount is 255");

                await box.ShowAsync();
                return;
            }
        }

        if (!bytes.SequenceEqual(progressValues))
        {
            memory.WriteProgress(bytes.ToArray());
        }
    }

    #endregion

    #region Initialization

    private void InitializeEvents()
    {
        eventStatuses = new byte[137];

        int primaryCount = 0, secondaryCount = 0;

        foreach (var ev in eventDtos)
        {
            var spl = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Width = 445,
                Height = 50
            };

            if (ev.Primary)
            {
                spl.Margin = new Thickness(0, 50 * primaryCount, 0, 0);
                primaryCount++;
            }
            else
            {
                spl.Margin = new Thickness(450, 50 * secondaryCount, 0, 0);
                secondaryCount++;
            }

            var lbl = new Label
            {
                Content = ev.Name,
                Width = 275,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 18,
                Tag = ev,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 230, 132))
            };

            var btnNotStarted = new Button
            {
                Content = "0",
                Width = 50,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 14,
                Tag = ev.Address,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
            };

            btnNotStarted.Click += OnClickNotStartedButton;

            var btnStarted = new Button
            {
                Content = "1",
                Width = 50,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 14,
                Tag = ev.Address,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
            };

            btnStarted.Click += OnClickStartedButton;

            var btnCompleted = new Button
            {
                Content = "255",
                Width = 50,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 14,
                Tag = ev.Address,
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
            };

            btnCompleted.Click += OnClickCompletedButton;

            btnNotStarted.Margin = new Thickness(5, 5, 0, 5);
            btnStarted.Margin = new Thickness(5, 5, 0, 5);
            btnCompleted.Margin = new Thickness(5, 5, 0, 5);

            spl.Children.Add(lbl);
            spl.Children.Add(btnNotStarted);
            spl.Children.Add(btnStarted);
            spl.Children.Add(btnCompleted);

            spl.Tag = ev.Address;

            Dispatcher.Invoke(() =>
            {
                CnvEvents.Children.Add(spl);
            });
        }

        CnvEvents.Height = eventDtos.Count(e => !e.Primary) * 50;
    }

    private void InitializeProgressGrid()
    {
        progressValues = new byte[168];
        for (var i = 0; i < progressValues.Length; i++)
        {
            var spl = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(20, 0, 0, 0)
            };

            var lbl = new Label
            {
                Content = $"0x{0xBF9B4 + i:X}",
                Width = 70,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };

            var txb = new TextBox
            {
                MinHeight = 18,
                MinWidth = 50,
                MaxWidth = 50,

                HorizontalContentAlignment = HorizontalAlignment.Right,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            spl.Children.Add(lbl);
            spl.Children.Add(txb);

            Grid.SetRow(spl, i % 21);
            Grid.SetColumn(spl, i / 21);

            GrdProgress.Children.Add(spl);
        }
    }

    #endregion

    private void ProcessLost(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            TabMain.IsSelected = true;
            TabInventory.IsEnabled = false;
            TabEvents.IsEnabled = false;
            TabTeleport.IsEnabled = false;
            TabProgress.IsEnabled = false;
        });

        findGameTimer.Start();
    }

    private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || memory == null) return;

        updateTabTimer.Tick -= TrackInventory;
        updateTabTimer.Tick -= TrackEvents;
        updateTabTimer.Tick -= TrackProgress;

        if (TabInventory.IsSelected)
        {
            updateTabTimer.Tick += TrackInventory;
        }
        else if (TabEvents.IsSelected)
        {
            updateTabTimer.Tick += TrackEvents;
        }
        else if (TabProgress.IsSelected)
        {
            updateTabTimer.Tick += TrackProgress;
        }
    }

    private async void BtnNewRandom_OnClick(object? sender, RoutedEventArgs e)
    {
        var timestamp = ((int)DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds).ToString();

        var topLevel = GetTopLevel(this);

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save seed",
            FileTypeChoices = [FilePickerFileTypes.TextPlain],
            SuggestedFileName = timestamp
        });

        if (file is not null)
        {
            await using var stream = await file.OpenWriteAsync();
            using var streamWriter = new StreamWriter(stream);

            randomizer = new Randomizer(itemDtos, areaDtos, eventDtos);
            randomizer.Randomize();
            var output = "";
            foreach (var item in randomizer.RandomizedItems)
            {
                output += $"{item.Key.Id},{item.Value.Id}|";
            }
            output = output.Remove(output.Length - 1);
            await streamWriter.WriteAsync(output);

            if (memory != null) memory.SetupRandomizer(randomizer);
        }

        if (ChkDebug.IsChecked == true)
        {
            var debugFile = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save debug file",
                FileTypeChoices = [FilePickerFileTypes.TextPlain],
                SuggestedFileName = $"{timestamp}_DEBUG"
            });

            if (debugFile is not null)
            {
                await using var stream = await debugFile.OpenWriteAsync();
                using var streamWriter = new StreamWriter(stream);

                await streamWriter.WriteAsync(randomizer.DebugString);

            }
        }
    }

    private async void BtnLoadRandom_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);

        var file = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Text File",
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.TextPlain]
        });

        if (file.Count == 1)
        {
            await using var stream = await file[0].OpenReadAsync();
            using var streamReader = new StreamReader(stream);

            var itemString = await streamReader.ReadToEndAsync();

            try
            {
                randomizer = new Randomizer(itemDtos, areaDtos, eventDtos);
                randomizer.Randomize(itemString);
                if (memory != null) memory.SetupRandomizer(randomizer);
            }
            catch (Exception)
            {
                var box = MessageBoxManager
                    .GetMessageBoxStandard("Error", "Invalid file");

                await box.ShowAsync();
            }
        }
    }

    private async void BtnJsonDeserializer_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save json stuff",
            FileTypeChoices = [FilePickerFileTypes.TextPlain]
        });

        if (file is not null)
        {
            await using var stream = await file.OpenWriteAsync();
            using var streamWriter = new StreamWriter(stream);

            int itemCount = 0, eventCount = 0, areaCount = 0;

            var output = "Items:\n\n";
            foreach (var item in itemDtos.Where(i => !i.NotRandom))
            {
                output += $"{++itemCount}) {item.Name}:\n\t";
                var reqGroups = item.Requirements;
                var reqGroupCount = 0;
                if (reqGroups == null) output += "No requirements\n";
                else
                {
                    foreach (var group in reqGroups)
                    {
                        output += (char)('a' + reqGroupCount++) + ")";
                        output += group.Items == null ? "" : "\tItems: " + string.Join(", ", group.Items.Select(i => itemDtos.First(id => i == id.Id).Name)) + "\n\t";
                        output += group.Events == null ? "" : "\tEvents: " + string.Join(", ", group.Events.Select(i => eventDtos.First(id => i == id.Id).Name)) + "\n\t";
                        output += group.Areas == null ? "" : "\tAreas: " + string.Join(", ", group.Areas.Select(i => areaDtos.First(id => i == id.Id).Name)) + "\n\t";
                    }
                }
                output += "\n";
            }

            output += "\nEvents:\n\n";
            foreach (var ev in eventDtos)
            {
                output += $"{++eventCount}) {ev.Name}:\n\t";
                var reqGroups = ev.Requirements;
                var reqGroupCount = 0;
                if (reqGroups == null) output += "No requirements\n";
                else
                {
                    foreach (var group in reqGroups)
                    {
                        output += (char)('a' + reqGroupCount++) + ")";
                        output += group.Items == null ? "" : "\tItems: " + string.Join(", ", group.Items.Select(i => itemDtos.First(id => i == id.Id).Name)) + "\n\t";
                        output += group.Events == null ? "" : "\tEvents: " + string.Join(", ", group.Events.Select(i => eventDtos.First(id => i == id.Id).Name)) + "\n\t";
                        output += group.Areas == null ? "" : "\tAreas: " + string.Join(", ", group.Areas.Select(i => areaDtos.First(id => i == id.Id).Name)) + "\n\t";
                    }
                }
                output += "\n";
            }

            output += "\nAreas::\n\n";
            foreach (var area in areaDtos)
            {
                output += $"{++areaCount}) {area.Name}:\n\t";
                var reqGroups = area.Requirements;
                var reqGroupCount = 0;
                if (reqGroups == null) output += "No requirements\n";
                else
                {
                    foreach (var group in reqGroups)
                    {
                        output += (char)('a' + reqGroupCount++) + ")";
                        output += group.Items == null ? "" : "\tItems: " + string.Join(", ", group.Items.Select(i => itemDtos.First(id => i == id.Id).Name)) + "\n\t";
                        output += group.Events == null ? "" : "\tEvents: " + string.Join(", ", group.Events.Select(i => eventDtos.First(id => i == id.Id).Name)) + "\n\t";
                        output += group.Areas == null ? "" : "\tAreas: " + string.Join(", ", group.Areas.Select(i => areaDtos.First(id => i == id.Id).Name)) + "\n\t";
                    }
                }
                output += "\n";
            }

            await streamWriter.WriteAsync(output);
        }
    }
}