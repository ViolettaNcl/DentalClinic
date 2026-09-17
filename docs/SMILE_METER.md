# Smile Meter

## Current implementation

The Cosmetic Treatments page contains an interactive Smile Meter with one visible dental model image at runtime. It does not composite multiple jaw photographs.

### Supported views

- **Front:** whitening, alignment and smile-shape stages.
- **Upper:** whitening and alignment stages.
- **Lower:** whitening and alignment stages.
- **Side:** whitening and alignment stages.

Shape is intentionally visualized in the Front view only because the current upper/lower/side source set does not contain a trustworthy shape progression.

### Image handling

- Source renders remain high-resolution PNG files.
- Runtime uses optimized WebP assets and lightweight thumbnails.
- Heavy model images are deferred until the Smile Meter approaches the viewport.
- Only the current and neighbouring relevant frames are warmed.
- Images are decoded before the committed swap.
- Stale transitions and preset/AI animations are cancellable.
- Manual slider movement changes only the nearest staged frame and does not use multi-layer crossfades.

### Camera-view integrity

Every staged image is classified as `front`, `upper`, `lower` or `side`, and runtime selection is constrained to the active camera family. This prevents upper/lower/side images from appearing in the wrong view.

### Responsive behaviour

The Smile Meter is designed for desktop, tablet and mobile layouts. Controls stack on narrow screens while the active model remains contained within the viewport.
