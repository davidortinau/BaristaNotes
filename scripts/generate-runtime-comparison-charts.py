#!/usr/bin/env python3

from html import escape
from pathlib import Path


OUTPUT_DIRECTORY = (
    Path(__file__).resolve().parents[1] / "docs" / "images" / "runtime-comparison"
)

BACKGROUND = "#F8FAFC"
PANEL = "#FFFFFF"
GRID = "#CBD5E1"
TEXT = "#0F172A"
MUTED = "#475569"

COLORS = {
    "MAUI full R2R": "#6D28D9",
    "MAUI partial R2R": "#A855F7",
    "MAUI Native AOT": "#DB2777",
    "Native full R2R": "#0369A1",
    "Native partial R2R": "#0EA5E9",
    "Native AOT": "#059669",
}


def text(x, y, value, size=14, weight=400, anchor="start", fill=TEXT, transform=None):
    transform_attribute = f' transform="{transform}"' if transform else ""
    return (
        f'<text x="{x}" y="{y}" font-family="Inter, Segoe UI, Arial, sans-serif" '
        f'font-size="{size}" font-weight="{weight}" text-anchor="{anchor}" '
        f'fill="{fill}"{transform_attribute}>{escape(str(value))}</text>'
    )


def line(x1, y1, x2, y2, stroke=GRID, width=1, dash=None):
    dash_attribute = f' stroke-dasharray="{dash}"' if dash else ""
    return (
        f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" '
        f'stroke="{stroke}" stroke-width="{width}"{dash_attribute}/>'
    )


def rect(x, y, width, height, fill, radius=0, stroke=None):
    stroke_attribute = f' stroke="{stroke}"' if stroke else ""
    return (
        f'<rect x="{x}" y="{y}" width="{width}" height="{height}" '
        f'rx="{radius}" fill="{fill}"{stroke_attribute}/>'
    )


def circle(cx, cy, radius, fill, stroke=PANEL, stroke_width=2):
    return (
        f'<circle cx="{cx}" cy="{cy}" r="{radius}" fill="{fill}" '
        f'stroke="{stroke}" stroke-width="{stroke_width}"/>'
    )


def document(title, subtitle, body, height=720):
    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="{height}" viewBox="0 0 1200 {height}" role="img" aria-labelledby="title description">
  <title id="title">{escape(title)}</title>
  <desc id="description">{escape(subtitle)}</desc>
  {rect(0, 0, 1200, height, BACKGROUND)}
  {text(50, 52, title, 28, 700)}
  {text(50, 80, subtitle, 15, 400, fill=MUTED)}
  {body}
</svg>
"""


def panel_frame(x, y, width, height, title_value):
    return [
        rect(x, y, width, height, PANEL, 12, GRID),
        text(x + 24, y + 38, title_value, 20, 700),
    ]


def horizontal_panel(x, y, width, height, title_value, items, maximum, unit, p90=False):
    elements = panel_frame(x, y, width, height, title_value)
    plot_x = x + 184
    plot_y = y + 82
    plot_width = width - 220
    plot_height = height - 116
    row_height = plot_height / len(items)

    for tick in range(0, 6):
        value = maximum * tick / 5
        tick_x = plot_x + plot_width * tick / 5
        elements.append(line(tick_x, plot_y - 14, tick_x, plot_y + plot_height))
        elements.append(text(tick_x, plot_y - 22, f"{value:g}", 11, anchor="middle", fill=MUTED))

    for index, item in enumerate(items):
        center_y = plot_y + row_height * index + row_height / 2
        bar_height = min(26, row_height * 0.45)
        value = item["p50"] if p90 else item["value"]
        bar_width = plot_width * value / maximum
        color = COLORS[item["label"]]
        elements.append(text(plot_x - 12, center_y + 5, item["label"], 12, 500, anchor="end"))
        elements.append(rect(plot_x, center_y - bar_height / 2, bar_width, bar_height, color, 5))
        label_inside = p90 or bar_width > plot_width * 0.82
        elements.append(
            text(
                plot_x + bar_width - 7 if label_inside else plot_x + bar_width + 7,
                center_y + 5,
                f"{value:g} {unit}",
                11,
                600,
                anchor="end" if label_inside else "start",
                fill=PANEL if label_inside else TEXT,
            )
        )
        if p90:
            marker_x = plot_x + plot_width * item["p90"] / maximum
            elements.append(line(marker_x, center_y - bar_height / 2 - 5, marker_x, center_y + bar_height / 2 + 5, TEXT, 2))
            elements.append(circle(marker_x, center_y, 4, TEXT))

    if p90:
        legend_y = y + height - 18
        elements.append(rect(x + 24, legend_y - 10, 20, 10, MUTED, 3))
        elements.append(text(x + 50, legend_y, "p50 bar", 11, fill=MUTED))
        elements.append(circle(x + 136, legend_y - 5, 4, TEXT))
        elements.append(text(x + 147, legend_y, "p90 marker", 11, fill=MUTED))

    return "\n  ".join(elements)


def grouped_bar_panel(
    x,
    y,
    width,
    height,
    title_value,
    categories,
    series,
    maximum,
    unit,
):
    elements = panel_frame(x, y, width, height, title_value)
    legend_rows = (len(series) + 1) // 2
    plot_x = x + 66
    plot_y = y + 78 + legend_rows * 20
    plot_width = width - 90
    baseline = y + height - 74
    plot_height = baseline - plot_y

    for tick in range(0, 6):
        value = maximum * tick / 5
        tick_y = baseline - plot_height * tick / 5
        elements.append(line(plot_x, tick_y, plot_x + plot_width, tick_y))
        elements.append(text(plot_x - 10, tick_y + 4, f"{value:g}", 10, anchor="end", fill=MUTED))

    group_width = plot_width / len(categories)
    bar_gap = 3
    bar_width = min(28, (group_width - 24) / len(series) - bar_gap)

    for category_index, category in enumerate(categories):
        group_center = plot_x + group_width * category_index + group_width / 2
        total_width = len(series) * (bar_width + bar_gap) - bar_gap
        group_start = group_center - total_width / 2

        for series_index, item in enumerate(series):
            value = item["values"][category_index]
            bar_height = plot_height * value / maximum
            bar_x = group_start + series_index * (bar_width + bar_gap)
            bar_y = baseline - bar_height
            elements.append(rect(bar_x, bar_y, bar_width, bar_height, COLORS[item["label"]], 3))
            if bar_width >= 25:
                elements.append(
                    text(
                        bar_x + bar_width / 2,
                        bar_y - 5,
                        f"{value:.1f}",
                        8,
                        600,
                        anchor="middle",
                    )
                )

        elements.append(text(group_center, baseline + 24, category, 11, 600, anchor="middle"))

    legend_x = x + 24
    legend_y = y + 65
    column_width = (width - 48) / 2
    for index, item in enumerate(series):
        row = index // 2
        column = index % 2
        item_x = legend_x + column * column_width
        item_y = legend_y + row * 20
        elements.append(rect(item_x, item_y - 10, 13, 13, COLORS[item["label"]], 2))
        elements.append(text(item_x + 19, item_y + 1, item["label"], 10, 500))

    elements.append(text(x + 22, baseline + 49, unit, 10, fill=MUTED))
    return "\n  ".join(elements)


def transition_panel(x, y, width, height, title_value, routes, maximum):
    elements = panel_frame(x, y, width, height, title_value)
    plot_x = x + 184
    plot_y = y + 76
    plot_width = width - 220
    total_rows = sum(len(route["items"]) for route in routes) + len(routes)
    row_height = (height - 112) / total_rows
    row_index = 0

    for tick in range(0, 6):
        value = maximum * tick / 5
        tick_x = plot_x + plot_width * tick / 5
        elements.append(line(tick_x, plot_y - 10, tick_x, y + height - 24))
        elements.append(text(tick_x, plot_y - 17, f"{value:g}", 10, anchor="middle", fill=MUTED))

    for route in routes:
        heading_y = plot_y + row_index * row_height + row_height * 0.65
        elements.append(text(x + 22, heading_y, route["name"], 11, 700))
        row_index += 1
        for item in route["items"]:
            center_y = plot_y + row_index * row_height + row_height / 2
            bar_height = min(18, row_height * 0.56)
            bar_width = plot_width * item["value"] / maximum
            elements.append(text(plot_x - 10, center_y + 4, item["label"], 10, 500, anchor="end"))
            elements.append(
                rect(
                    plot_x,
                    center_y - bar_height / 2,
                    bar_width,
                    bar_height,
                    COLORS[item["label"]],
                    4,
                )
            )
            label_inside = bar_width > plot_width * 0.82
            elements.append(
                text(
                    plot_x + bar_width - 6 if label_inside else plot_x + bar_width + 6,
                    center_y + 4,
                    f'{item["value"]:g} ms',
                    10,
                    600,
                    anchor="end" if label_inside else "start",
                    fill=PANEL if label_inside else TEXT,
                )
            )
            row_index += 1

    return "\n  ".join(elements)


def write_chart(filename, title, subtitle, body, height=720):
    OUTPUT_DIRECTORY.mkdir(parents=True, exist_ok=True)
    (OUTPUT_DIRECTORY / filename).write_text(
        document(title, subtitle, body, height),
        encoding="utf-8",
    )


def generate_startup_chart():
    ios = [
        {"label": "MAUI full R2R", "p50": 490.3, "p90": 505.3},
        {"label": "MAUI Native AOT", "p50": 321.8, "p90": 356.1},
        {"label": "Native full R2R", "p50": 319.0, "p90": 334.3},
        {"label": "Native AOT", "p50": 185.9, "p90": 200.3},
    ]
    android = [
        {"label": "MAUI full R2R", "p50": 973.5, "p90": 1011},
        {"label": "MAUI partial R2R", "p50": 1132.0, "p90": 1166},
        {"label": "MAUI Native AOT", "p50": 280.0, "p90": 908},
        {"label": "Native full R2R", "p50": 556.5, "p90": 579},
        {"label": "Native partial R2R", "p50": 939.0, "p90": 984},
        {"label": "Native AOT", "p50": 181.0, "p90": 196},
    ]
    body = horizontal_panel(40, 110, 550, 540, "iOS - DX24", ios, 550, "ms", True)
    body += "\n  " + horizontal_panel(
        610, 110, 550, 540, "Android - Pixel 5", android, 1200, "ms", True
    )
    write_chart(
        "startup.svg",
        "Process-cold startup",
        "Lower is better. Bars show p50. Markers show p90.",
        body,
    )


def generate_package_chart():
    ios = [
        {"label": "MAUI full R2R", "value": 58.86},
        {"label": "MAUI Native AOT", "value": 42.01},
        {"label": "Native full R2R", "value": 49.05},
        {"label": "Native AOT", "value": 32.06},
    ]
    android = [
        {"label": "MAUI full R2R", "value": 45.65},
        {"label": "MAUI partial R2R", "value": 28.71},
        {"label": "MAUI Native AOT", "value": 30.40},
        {"label": "Native full R2R", "value": 31.72},
        {"label": "Native partial R2R", "value": 16.29},
        {"label": "Native AOT", "value": 18.52},
    ]
    body = horizontal_panel(40, 110, 550, 520, "iOS signed IPA", ios, 65, "MiB")
    body += "\n  " + horizontal_panel(
        610, 110, 550, 520, "Android signed APK", android, 50, "MiB"
    )
    write_chart(
        "package-size.svg",
        "Signed benchmark package size",
        "Lower is better. Each package contains the accepted fixture and benchmark probes.",
        body,
        700,
    )


def generate_memory_chart():
    categories = ["Initial", "Activity", "Repeated"]
    ios = [
        {"label": "MAUI full R2R", "values": [80.96, 98.04, 127.21]},
        {"label": "MAUI Native AOT", "values": [65.38, 80.84, 113.77]},
        {"label": "Native full R2R", "values": [60.75, 60.94, 64.37]},
        {"label": "Native AOT", "values": [51.85, 52.39, 50.84]},
    ]
    android = [
        {"label": "MAUI full R2R", "values": [358.99, 374.32, 388.55]},
        {"label": "MAUI partial R2R", "values": [206.81, 211.78, 223.63]},
        {"label": "MAUI Native AOT", "values": [164.26, 172.12, 180.58]},
        {"label": "Native full R2R", "values": [276.83, 285.89, 304.23]},
        {"label": "Native partial R2R", "values": [138.64, 137.63, 152.49]},
        {"label": "Native AOT", "values": [117.58, 123.61, 133.63]},
    ]
    body = grouped_bar_panel(
        40, 110, 550, 540, "iOS physical footprint", categories, ios, 140, "MiB"
    )
    body += "\n  " + grouped_bar_panel(
        610, 110, 550, 540, "Android proportional set size", categories, android, 420, "MiB"
    )
    write_chart(
        "memory.svg",
        "Memory by application state",
        "Lower is better. Platform memory measures are not comparable to each other.",
        body,
    )


def generate_transition_chart():
    ios = [
        {
            "name": "New Drink to Activity",
            "items": [
                {"label": "MAUI full R2R", "value": 483.4},
                {"label": "MAUI Native AOT", "value": 300.1},
                {"label": "Native full R2R", "value": 98.3},
                {"label": "Native AOT", "value": 65.2},
            ],
        },
        {
            "name": "Activity to Settings",
            "items": [
                {"label": "MAUI full R2R", "value": 133.3},
                {"label": "MAUI Native AOT", "value": 133.3},
                {"label": "Native full R2R", "value": 48.1},
                {"label": "Native AOT", "value": 60.8},
            ],
        },
    ]
    android = [
        {
            "name": "New Drink to Activity",
            "items": [
                {"label": "MAUI full R2R", "value": 403.0},
                {"label": "MAUI partial R2R", "value": 546.9},
                {"label": "MAUI Native AOT", "value": 270.4},
                {"label": "Native full R2R", "value": 404.3},
                {"label": "Native partial R2R", "value": 677.2},
                {"label": "Native AOT", "value": 225.5},
            ],
        },
        {
            "name": "Activity to Settings",
            "items": [
                {"label": "MAUI full R2R", "value": 42.7},
                {"label": "MAUI partial R2R", "value": 46.2},
                {"label": "MAUI Native AOT", "value": 21.1},
                {"label": "Native full R2R", "value": 63.6},
                {"label": "Native partial R2R", "value": 91.0},
                {"label": "Native AOT", "value": 45.7},
            ],
        },
    ]
    body = transition_panel(40, 110, 550, 540, "iOS p50", ios, 550)
    body += "\n  " + transition_panel(610, 110, 550, 540, "Android p50", android, 700)
    write_chart(
        "transitions.svg",
        "Screen transition time",
        "Lower is better. Each value is the median of ten fresh-process samples.",
        body,
    )


def main():
    generate_startup_chart()
    generate_package_chart()
    generate_memory_chart()
    generate_transition_chart()
    print(f"Generated charts in {OUTPUT_DIRECTORY}")


if __name__ == "__main__":
    main()
