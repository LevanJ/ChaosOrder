# ChaosOrder

A WPF (.NET 8) desktop app for exploring the **chaos game** — the algorithm behind the
Sierpinski triangle. Pick a polygon, a starting point, and a set of movement rules, then
run tens of thousands of iterations to watch the resulting fractal emerge.

## How it works

Starting from a point, each iteration:
1. Randomly picks one of the enabled **rules** (weighted).
2. The rule selects a **target point** — a corner of the figure, the midpoint of a side,
   or the 1/n-th point along a side.
3. The current point jumps some **fraction of the remaining distance** toward that target.
4. The new point is plotted, and the process repeats.

The classic Sierpinski triangle is the special case: 3 corners, target = corners, step = 1/2.
ChaosOrder generalizes this to any polygon, any mix of target types, and any step ratio
(including values greater than 1, which overshoot past the target).

## Features

- **Configurable figure** — choose a preset (triangle, square, pentagon, hexagon) or build
  a custom polygon by adding/removing corners.
- **Direct manipulation** — drag corners and the starting point right on the canvas, or
  edit their coordinates numerically in the corners grid / starting point fields.
- **Make Regular** — snap the current corners (however many, however edited) into a
  regular N-gon, centered exactly on the canvas and sized to fill it (with padding). Also
  recenters the starting point and resets zoom.
- **Movement rules** — add any number of rules, each with:
  - a target type (corners / middle of sides / 1/n-th point of sides),
  - a step, entered as a fraction or plain number (e.g. `1/2`, `2/3`, `0.5`, `2`),
  - a relative weight for random selection,
  - an enable/disable toggle.
- **Simulation controls** — set the number of points to plot, and choose the line/dot
  color and thickness.
- **Zoom** — Ctrl + Mouse Wheel zooms the figure and plotted points in place, without
  resizing the canvas frame.
- **Save/Load configurations** — every setting (figure, corners, starting point, rules,
  color, thickness) can be saved to a single JSON store (`configurations.json`, checked
  into this repo) under an auto-generated short name based on corner count, rule steps,
  and date. Saved configurations are listed and reloadable from within the app; a manual
  "Load from File..." option also supports importing a standalone configuration file.

## Running

Open `ChaosOrder.sln` in Visual Studio (or run `dotnet build` / `dotnet run` from the
`ChaosOrder` project folder) — requires the .NET 8 SDK with WPF support on Windows.
