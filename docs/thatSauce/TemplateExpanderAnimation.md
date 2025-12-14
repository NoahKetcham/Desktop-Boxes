# Template Expander Animation (Avalonia) — Notes + Reusable Pattern

This document explains the expand/collapse animation used for the Dashboard “Templates” section, why certain approaches were flaky, and how to reuse the final approach in other parts of the app.

## What we were animating

We wanted a per-row “Templates” section to:
- Expand/collapse smoothly.
- Work reliably inside a `ListBox` that uses a `VirtualizingStackPanel`.
- Avoid layout flicker at the end of the animation.
- Not break interaction when “collapsed” (no stray clicks on hidden content).

## The two approaches we tried

### Approach A: Dynamic-height animation (`AnimatedCollapsePresenter`)

**Idea**: measure content height, animate `Height` 0 → measuredHeight and `Opacity` 0 → 1.

**Pros**
- Perfect “fit content” height (no guessing).
- Can feel very premium when it works.

**Cons (why it became flaky in our case)**
- Under list virtualization, controls can be measured/arranged at unexpected times.
- If you hide the content with `IsVisible=false`, the next expand may not reliably re-measure/arrange, depending on container recycling.
- Switching to `Height=Auto` (NaN) right after animation can cause a one-frame “flash” if the layout pass resolves differently.

We mitigated some issues by:
- Avoiding `IsVisible` toggles (keeping content in the visual tree and collapsing via `Height=0`, `Opacity=0`, `IsHitTestVisible=false`).
- Adding a small debounce to reduce rapid-toggle jitter.
- Avoiding an immediate `Height=Auto` swap after expand.

But the combination of virtualization + “measure-to-animate” still proved more fragile than desirable for this UI.

### Approach B (Final): Animate `MaxHeight` + `Opacity` (simple + reliable)

**Idea**: keep the content always present, and animate a wrapper’s `MaxHeight` and `Opacity` based on a boolean.

This is the technique currently used in:
- `Boxes.App/Views/DashboardPageView.axaml`
- with converters in:
  - `Boxes.App/Converters/BoolToDoubleConverter.cs`
  - resources registered in `Boxes.App/App.axaml`

**Pros**
- Extremely reliable under `VirtualizingStackPanel` recycling.
- No dependence on “perfect measurement timing”.
- Easy to reason about and debug.

**Cons**
- You must choose a “large enough” expanded `MaxHeight`. (We used `420` as a safe value for the current tiles layout.)

## The reusable recipe (copy/paste)

### 1) Add a bool → double converter

If you don’t already have one, the project uses:
- `Boxes.App/Converters/BoolToDoubleConverter.cs`

Example:

```csharp
public class BoolToDoubleConverter : IValueConverter
{
    public double TrueValue { get; set; } = 1;
    public double FalseValue { get; set; } = 0;
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueValue : FalseValue;
}
```

### 2) Register resources (app-wide)

In `Boxes.App/App.axaml` add resources like:

```xml
<conv:BoolToDoubleConverter x:Key="BoolToMySectionMaxHeight" TrueValue="420" FalseValue="0" />
<conv:BoolToDoubleConverter x:Key="BoolToOpacity" TrueValue="1" FalseValue="0" />
```

Tune `TrueValue` (max height) per section.

### 3) Wrap your collapsible content in a `Border` and animate `MaxHeight`

```xml
<Border ClipToBounds="True"
        MaxHeight="{Binding IsExpanded, Converter={StaticResource BoolToMySectionMaxHeight}}"
        Opacity="{Binding IsExpanded, Converter={StaticResource BoolToOpacity}}"
        IsHitTestVisible="{Binding IsExpanded}">

  <Border.Transitions>
    <Transitions>
      <DoubleTransition Property="MaxHeight" Duration="0:0:0.38"/>
      <DoubleTransition Property="Opacity" Duration="0:0:0.18"/>
    </Transitions>
  </Border.Transitions>

  <!-- Content to reveal -->
  <Border BorderThickness="0,1,0,0" Padding="16">
    <!-- ... -->
  </Border>
</Border>
```

### Why `IsHitTestVisible` matters

When collapsed, `Opacity=0` alone would still allow click/hover events to hit invisible controls. Binding:

```xml
IsHitTestVisible="{Binding IsExpanded}"
```

prevents invisible content from stealing interactions.

## Tuning recommendations

- **Expand duration**: `0.30s–0.45s` feels smooth.
- **Opacity duration**: shorter than height (e.g., `0.12s–0.20s`) to avoid “ghosting”.
- **MaxHeight**:
  - Pick a value that covers worst-case content height.
  - If content might grow beyond it later, increase `TrueValue`.

## Common pitfalls

- **Using `IsVisible=false` for collapse inside virtualized lists**:
  - Can prevent reliable measurement/arrangement on re-expand.
  - Prefer “keep in tree” + `MaxHeight/Opacity/IsHitTestVisible`.

- **Animating `Height` to `Auto`**:
  - Switching `Height` to `Auto` after an animation can cause a one-frame jump.
  - If you must animate height, keep a stable numeric height after expand, or delay the switch to auto until after a layout pass.

## Where this is used in Desktop-Boxes

- `Boxes.App/Views/DashboardPageView.axaml`: the template expander section.
- `Boxes.App/Converters/BoolToDoubleConverter.cs`: bool→double for height/opacity.
- `Boxes.App/App.axaml`: shared resources for the converters.


