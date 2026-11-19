import argparse
import json
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.lines import Line2D

parser = argparse.ArgumentParser()
parser.add_argument("results", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
cells = {}
for folder in args.results.iterdir():
    if folder.is_dir() and (folder / "summary.json").exists():
        value = json.loads((folder / "summary.json").read_text())
        assert value["status"] == "complete"
        cells[folder.name] = value
assert set(cells) == {
    f"{app}-{variant}"
    for app in ("maui", "native")
    for variant in ("fullr2r", "partialr2r", "aot")
} | {
    f"ios-{app}-{variant}"
    for app in ("maui", "native")
    for variant in ("fullr2r", "aot")
}
args.output.mkdir(parents=True, exist_ok=True)

BLUE, ORANGE, INK, GRID = "#2a78d6", "#eb6834", "#111111", "#e1e0d9"
plt.rcParams.update({
    "font.family": ["Helvetica Neue", "Arial", "DejaVu Sans"],
    "font.size": 12,
    "figure.facecolor": "white",
    "axes.facecolor": "white",
    "text.color": INK,
    "axes.labelcolor": INK,
    "xtick.color": "#52514e",
})
labels = ["Full composite R2R", "Partial R2R\n(configured profiles)", "NativeAOT"]
variants = ["fullr2r", "partialr2r", "aot"]


def panel(ax, rows, title, unit, decimals=0):
    maximum = max(max(row[1:]) for row in rows) * 1.3
    for i, (label, maui, native) in enumerate(rows):
        y = len(rows) - i - 1
        if i == 0:
            ax.axhspan(y - 0.4, y + 0.4, color="#f3f2ed", zorder=0)
        close = abs(maui - native) / maximum < 0.1
        maui_y, native_y = (y + 0.12, y - 0.12) if close else (y, y)
        ax.plot([maui, native], [maui_y, native_y], color="#77766f", linewidth=2, zorder=2)
        ax.scatter(maui, maui_y, color=BLUE, marker="o", s=110, edgecolor="white", linewidth=1.5, zorder=3)
        ax.scatter(native, native_y, color=ORANGE, marker="s", s=90, edgecolor="white", linewidth=1.5, zorder=3)
        if close:
            right_edge = max(maui, native) / maximum > 0.65
            x = min(maui, native) - maximum * 0.025 if right_edge else max(maui, native) + maximum * 0.025
            ax.text(x, y,
                    f"{maui:,.{decimals}f} / {native:,.{decimals}f}",
                    ha="right" if right_edge else "left", va="center", fontsize=11, fontweight="bold")
        else:
            for value in (maui, native):
                ax.text(value, y + 0.19, f"{value:,.{decimals}f}",
                        ha="center", va="bottom", fontsize=11, fontweight="bold")
    ax.set_yticks(range(len(rows) - 1, -1, -1), [r[0] for r in rows])
    ax.set_xlim(0, maximum)
    ax.set_ylim(-0.65, len(rows) - 0.2)
    ax.set_title(title, loc="left", fontsize=14, fontweight="bold", pad=18)
    ax.set_xlabel(unit)
    ax.tick_params(length=0, pad=6)
    ax.grid(axis="x", color=GRID)
    ax.set_axisbelow(True)
    for side in ("left", "right", "top"):
        ax.spines[side].set_visible(False)
    ax.spines["bottom"].set_color("#898781")


def frame(title, rows=1):
    fig, axes = plt.subplots(rows, 2, figsize=(11, 5.3 if rows == 1 else 8.3), squeeze=False)
    fig.subplots_adjust(left=0.19, right=0.98, top=0.69 if rows == 1 else 0.76,
                        bottom=0.24 if rows == 1 else 0.18, wspace=0.94, hspace=0.85)
    fig.text(0.015, 0.97, title, fontsize=19, fontweight="bold", va="top")
    fig.text(0.015, 0.90 if rows == 1 else 0.925,
             "DevFlow-instrumented builds. Matched full R2R is shaded. Lower is better.",
             fontsize=12, va="top")
    fig.legend(
        [Line2D([], [], marker="o", linestyle="", color=BLUE, markersize=9),
         Line2D([], [], marker="s", linestyle="", color=ORANGE, markersize=8)],
        [".NET MAUI (MauiReactor)", "Platform UI in C#"],
        loc="upper left", bbox_to_anchor=(0.015, 0.84 if rows == 1 else 0.88),
        frameon=False, ncol=2, fontsize=12,
    )
    fig.text(0.015, 0.018,
             ".NET 11 GA. iOS: iPhone 15 Pro. Android: Pixel 5. Same 1,000-drink fixture.\n"
             "Compare within each panel; startup clocks differ by OS. Close pairs: .NET MAUI / platform UI.",
             fontsize=10, color="#52514e", va="bottom")
    return fig, axes


def android_rows(metric):
    return [(label, metric(cells["maui-" + variant]), metric(cells["native-" + variant]))
            for label, variant in zip(labels, variants)]


def ios_rows(metric):
    return [(label, metric(cells["ios-maui-" + variant]), metric(cells["ios-native-" + variant]))
            for label, variant in (("Full composite R2R", "fullr2r"), ("NativeAOT", "aot"))]


fig, axes = frame("Process-cold startup: median of 30 launches")
panel(axes[0][0], ios_rows(lambda c: c["startup"]["p50"]), "iOS (GA)", "milliseconds", 1)
panel(axes[0][1], android_rows(lambda c: c["startup"]["p50"]), "Android (GA)", "milliseconds", 1)
fig.savefig(args.output / "startup.png", dpi=180)
plt.close(fig)

fig, axes = frame("Signed benchmark package size")
panel(axes[0][0], ios_rows(lambda c: c["bytes"] / 2**20), "iOS IPA (GA)", "MiB", 1)
panel(axes[0][1], android_rows(lambda c: c["bytes"] / 2**20),
      "Android arm64-only APK (GA)", "MiB", 1)
fig.savefig(args.output / "package-size.png", dpi=180)
plt.close(fig)

fig, axes = frame("Screen navigation: median of 10 fresh-process samples", rows=2)
for column, (route, name) in enumerate([
    ("new-drink-to-activity", "New Drink to history"),
    ("activity-to-settings", "History to Settings"),
]):
    panel(axes[0][column], ios_rows(lambda c: c["transitions"][route]["p50"]),
          "iOS (GA): " + name, "milliseconds", 1)
    panel(axes[1][column], android_rows(lambda c: c["transitions"][route]["p50"]),
          "Android (GA): " + name, "milliseconds", 1)
fig.savefig(args.output / "transitions.png", dpi=180)
plt.close(fig)
print("Rendered three DevFlow-instrumented runtime charts.")
