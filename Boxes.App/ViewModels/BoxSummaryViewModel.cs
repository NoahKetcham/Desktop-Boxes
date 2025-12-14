using System;
using System.Collections.Generic;
using Boxes.App.Models;

namespace Boxes.App.ViewModels;

public class BoxSummaryViewModel : ViewModelBase
{
    public Guid Id { get; private set; }

    private bool _isTemplatesExpanded;
    public bool IsTemplatesExpanded
    {
        get => _isTemplatesExpanded;
        set => SetProperty(ref _isTemplatesExpanded, value);
    }

    private string? _templateKey;
    public string? TemplateKey
    {
        get => _templateKey;
        set => SetProperty(ref _templateKey, value);
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private int _itemCount;
    public int ItemCount
    {
        get => _itemCount;
        set => SetProperty(ref _itemCount, value);
    }

    public List<Guid> ShortcutIds { get; set; } = new();

    public static BoxSummaryViewModel FromModel(DesktopBox model)
    {
        return new BoxSummaryViewModel
        {
            Id = model.Id,
            Name = model.Name,
            Description = model.Description,
            ItemCount = model.ItemCount,
            ShortcutIds = new List<Guid>(model.ShortcutIds),
            TemplateKey = model.TemplateKey,
            _width = model.Width,
            _height = model.Height,
            _positionX = model.PositionX,
            _positionY = model.PositionY,
            _currentPath = model.CurrentPath
        };
    }

    public void UpdateFromModel(DesktopBox model)
    {
        Id = model.Id;
        Name = model.Name;
        Description = model.Description;
        ItemCount = model.ItemCount;
        ShortcutIds = new List<Guid>(model.ShortcutIds);
        TemplateKey = model.TemplateKey;
        _width = model.Width;
        _height = model.Height;
        _positionX = model.PositionX;
        _positionY = model.PositionY;
        _currentPath = model.CurrentPath;
    }

    public DesktopBox ToModel()
    {
        return new DesktopBox
        {
            Id = Id,
            Name = Name,
            Description = Description,
            ItemCount = ItemCount,
            ShortcutIds = new List<Guid>(ShortcutIds),
            TemplateKey = TemplateKey,
            Width = _width > 0 ? _width : 320,
            Height = _height > 0 ? _height : 240,
            PositionX = _positionX,
            PositionY = _positionY,
            CurrentPath = _currentPath
        };
    }

    private double _width = 320;
    private double _height = 240;
    private double? _positionX;
    private double? _positionY;
    private string? _currentPath;
}

