using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Boxes.App.ViewModels.Widgets;

namespace Boxes.App.Views.Controls;

public partial class NotepadEditorPanel : UserControl
{
    public NotepadEditorPanel()
    {
        InitializeComponent();
    }

    private NotepadWindowViewModel ViewModel => (NotepadWindowViewModel)DataContext!;

    private void InsertMarkdownWrap(string before, string after)
    {
        var tb = this.FindControl<TextBox>("EditorTextBox");
        if (tb == null || DataContext is not NotepadWindowViewModel vm) return;

        var text = vm.MarkdownText;
        var start = Math.Min(tb.SelectionStart, tb.SelectionEnd);
        var end = Math.Max(tb.SelectionStart, tb.SelectionEnd);
        var selected = start < end ? text.Substring(start, end - start) : "";

        string newText;
        int newCaret;
        if (selected.Length > 0)
        {
            newText = text.Substring(0, start) + before + selected + after + text.Substring(end);
            newCaret = start + before.Length + selected.Length + after.Length;
        }
        else
        {
            newText = text.Substring(0, start) + before + after + text.Substring(start);
            newCaret = start + before.Length;
        }

        vm.MarkdownText = newText;
        tb.CaretIndex = newCaret;
        tb.SelectionStart = newCaret;
        tb.SelectionEnd = newCaret;
    }

    private void InsertMarkdownLinePrefix(string prefix)
    {
        var tb = this.FindControl<TextBox>("EditorTextBox");
        if (tb == null || DataContext is not NotepadWindowViewModel vm) return;

        var text = vm.MarkdownText;
        var caret = tb.CaretIndex;
        var searchFrom = caret > 0 ? caret - 1 : 0;
        var lineStart = text.LastIndexOf('\n', searchFrom) + 1;
        var newText = text.Substring(0, lineStart) + prefix + text.Substring(lineStart);
        vm.MarkdownText = newText;
        tb.CaretIndex = caret + prefix.Length;
        tb.SelectionStart = tb.CaretIndex;
        tb.SelectionEnd = tb.CaretIndex;
    }

    private void InsertMarkdownColorStyle(string style)
    {
        InsertMarkdownWrap($"%{{{style}}}", "%");
    }

    private void InsertMarkdownBlock(string block)
    {
        var tb = this.FindControl<TextBox>("EditorTextBox");
        if (tb == null || DataContext is not NotepadWindowViewModel vm) return;

        var text = vm.MarkdownText;
        var caret = tb.CaretIndex;
        var insert = (caret > 0 && text[caret - 1] != '\n' ? "\n" : "") + block + "\n";
        var newText = text.Substring(0, caret) + insert + text.Substring(caret);
        vm.MarkdownText = newText;
        tb.CaretIndex = caret + insert.Length;
        tb.SelectionStart = tb.CaretIndex;
        tb.SelectionEnd = tb.CaretIndex;
    }

    private void MarkdownBold_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownWrap("**", "**");
    private void MarkdownItalic_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownWrap("*", "*");
    private void MarkdownStrikethrough_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownWrap("~~", "~~");
    private void MarkdownCode_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownWrap("`", "`");
    private void MarkdownCodeBlock_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownWrap("\n```\n", "\n```\n");
    private void MarkdownLink_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownWrap("[", "](url)");
    private void MarkdownHeading1_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("# ");
    private void MarkdownHeading2_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("## ");
    private void MarkdownHeading3_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("### ");
    private void MarkdownBullet_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("- ");
    private void MarkdownNumbered_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("1. ");
    private void MarkdownBlockquote_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("> ");
    private void MarkdownHr_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownBlock("---");
    private void MarkdownHighlight_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownColorStyle("background:yellow");
    private void MarkdownColorRed_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownColorStyle("color:red");
    private void MarkdownColorGreen_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownColorStyle("color:green");
    private void MarkdownColorBlue_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownColorStyle("color:blue");
    private void MarkdownColorOrange_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownColorStyle("color:orange");
    private void MarkdownColorYellow_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownColorStyle("color:yellow");
}
