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
        assert value["status"] in ("complete", "timing-complete-memory-incomplete")
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
        ax.plot([maui, native], [y, y], color="#77766f", linewidth=2, zorder=2)
        ax.scatter(maui, y, color=BLUE, marker="o", s=110, edgecolor="white", linewidth=1.5, zorder=3)
        ax.scatter(native, y, color=ORANGE, marker="s", s=90, edgecolor="white", linewidth=1.5, zorder=3)
        if abs(maui - native) / maximum < 0.1:
            ax.text(max(maui, native) + maximum * 0.025, y,
                    f"{maui:,.{decimals}f} / {native:,.{decimals}f}",
                    va="center", fontsize=11, fontweight="bold")
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
             "Matched full R2R is shaded. Android shipping defaults differ. Lower is better.",
             fontsize=12, va="top")
    fig.legend(
        [Line2D([], [], marker="o", linestyle="", color=BLUE, markersize=9),
         Line2D([], [], marker="s", linestyle="", color=ORANGE, markersize=8)],
        [".NET MAUI (MauiReactor)", "Platform UI in C#"],
        loc="upper left", bbox_to_anchor=(0.015, 0.84 if rows == 1 else 0.88),
        frameon=False, ncol=2, fontsize=12,
    )
    fig.text(0.015, 0.018,
             ".NET 11 GA (SDK 11.0.100). iOS: iPhone 15 Pro. Android: Pixel 5. Same 1,000-drink fixture.\n"
             "Compare implementations within each panel. Close pairs are labeled .NET MAUI / platform UI.",
             fontsize=10, color="#52514e", va="bottom")
    return fig, axes


def android_rows(metric):
    return [(label, metric(cells["maui-" + variant]), metric(cells["native-" + variant]))
            for label, variant in zip(labels, variants)]


def ios_rows(metric):
    return [(label, metric(cells["ios-maui-" + variant]), metric(cells["ios-native-" + variant]))
            for label, variant in (("Full composite R2R", "fullr2r"), ("NativeAOT", "aot"))]


fig, axes = frame("Process-cold startup: median of 30 launches")
panel(axes[0][0], ios_rows(lambda c: c["startup"]["p50"]), "iOS (GA)", "milliseconds")
panel(axes[0][1], android_rows(lambda c: c["startup"]["p50"]), "Android (GA)", "milliseconds")
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
          "iOS (GA): " + name, "milliseconds")
    panel(axes[1][column], android_rows(lambda c: c["transitions"][route]["p50"]),
          "Android (GA): " + name, "milliseconds")
fig.savefig(args.output / "transitions.png", dpi=180)
plt.close(fig)
fig, ax = plt.subplots(figsize=(10, 5.5))
fig.subplots_adjust(left=0.30, right=0.94, top=0.76, bottom=0.18)
fig.text(0.015, 0.965, "Adding the bean map: platform UI vs .NET MAUI",
         fontsize=19, fontweight="bold", va="top")
fig.text(0.015, 0.895,
         "Recorded worker measures, with both platform heads and shared code counted once.",
         fontsize=12, va="top")
ratios = [
    ("Model minutes", 121.160 / 87.649),
    ("Model calls", 368 / 254),
    ("Gross input tokens\n(cache included)", 17283666 / 10041328),
    ("Output tokens", 374680 / 260799),
    ("Product lines added", 2795 / 2027),
    ("Test lines added", 1286 / 946),
    ("Feedback items", 19 / 13),
]
ax.axvline(1, color=BLUE, linewidth=2)
ax.axvline(2, color="#666666", linewidth=1.5, linestyle="--")
for y, (label, ratio) in enumerate(reversed(ratios)):
    ax.plot([1, ratio], [y, y], color=ORANGE, linewidth=2)
    ax.scatter(ratio, y, marker="s", color=ORANGE, edgecolor="white", s=95, zorder=3)
    ax.text(ratio + 0.025, y, f"{ratio:.2f}x", va="center", fontweight="bold")
ax.set_yticks(range(len(ratios)), [r[0] for r in reversed(ratios)])
ax.set_xticks([1, 1.25, 1.5, 1.75, 2], ["1.0x", "1.25x", "1.5x", "1.75x", "2.0x"])
ax.set_xlim(0.95, 2.08)
ax.set_ylim(-0.5, 6.5)
ax.grid(axis="x", color=GRID)
ax.set_axisbelow(True)
ax.tick_params(length=0, pad=8)
for side in ("left", "right", "top"):
    ax.spines[side].set_visible(False)
ax.spines["bottom"].set_color("#898781")
fig.text(0.015, 0.025,
         "Frozen October 3, 2026, before final acceptance. Same worker model and scope.\n"
         "Cache categories are included in gross input; these ratios are not dollar-cost ratios.",
         fontsize=11, color="#52514e", va="bottom")
fig.savefig(args.output / "map-feature-ratio.png", dpi=180)
plt.close(fig)
print("Rendered three runtime charts and the cache-labeled feature comparison chart.")
