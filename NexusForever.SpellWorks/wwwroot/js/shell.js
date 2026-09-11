// The whole interop surface. Nothing here runs per render, per pointer-move round trip, or per row:
// gestures are handled in JS and report to .NET exactly once, on release.

let hotkeyTarget = null;

export function registerHotkeys(dotnet) {
    hotkeyTarget = dotnet;

    // On document, not on a Blazor element - a @onkeydown misses keys whenever focus sits in an input.
    document.addEventListener('keydown', onKeyDown, true);
    document.addEventListener('contextmenu', e => e.preventDefault());
    window.addEventListener('blur', () => hotkeyTarget && hotkeyTarget.invokeMethodAsync('OnWindowBlur'));
}

function onKeyDown(e) {
    if (!hotkeyTarget) return;

    const key = (e.key || '').toLowerCase();

    if (key === 'k' && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        hotkeyTarget.invokeMethodAsync('OnHotkey', 'palette', false);
        return;
    }

    if (key === 'escape') {
        hotkeyTarget.invokeMethodAsync('OnHotkey', 'escape', false);
        return;
    }

    if (key === 'enter') {
        hotkeyTarget.invokeMethodAsync('OnHotkey', 'enter', e.shiftKey);
        return;
    }

    if (key === 'arrowdown' || key === 'arrowup') {
        // Only meaningful while the palette or a menu is open; .NET decides and ignores it otherwise.
        hotkeyTarget.invokeMethodAsync('OnHotkey', key === 'arrowdown' ? 'down' : 'up', false);
    }
}

/** Measure a floating surface and clamp it inside the viewport. WPF gave this away for free; the browser does not. */
export function menuPosition(x, y, width, height) {
    const pad = 6;
    return {
        x: Math.max(pad, Math.min(x, window.innerWidth - width - pad)),
        y: Math.max(pad, Math.min(y, window.innerHeight - height - pad))
    };
}

export function measure(element) {
    if (!element) return { width: 0, height: 0 };
    const rect = element.getBoundingClientRect();
    return { width: rect.width, height: rect.height };
}

export function focus(element) {
    if (element) element.focus();
}

export function copyText(text) {
    return navigator.clipboard.writeText(String(text ?? ''));
}

/**
 * Shared gesture plumbing.
 *
 * The `event` argument is a Blazor PointerEventArgs marshalled through JSON, NOT a DOM event: it carries
 * coordinates and nothing else. It has no `target`, so pointer capture is impossible and every listener
 * goes on the document instead. Capture-phase listeners keep the gesture alive over iframes and buttons.
 */
function dragSession(onMove, onEnd, cursor) {
    const previousCursor = document.body.style.cursor;

    document.body.style.cursor = cursor || '';
    document.body.style.userSelect = 'none';

    const move = e => onMove(e);

    const end = e => {
        document.removeEventListener('pointermove', move, true);
        document.removeEventListener('pointerup', end, true);
        document.removeEventListener('pointercancel', end, true);
        document.body.style.cursor = previousCursor;
        document.body.style.userSelect = '';

        // A cancelled gesture delivers no click, so the handlers know not to arm one to be swallowed.
        onEnd(e, e.type !== 'pointercancel');
    };

    document.addEventListener('pointermove', move, true);
    document.addEventListener('pointerup', end, true);
    document.addEventListener('pointercancel', end, true);
}

/**
 * Swallow the click that a completed drag would otherwise deliver to whatever sits under the pointer.
 *
 * Armed only for the moment the click would arrive in. A `once` listener left waiting for a click that
 * never comes - a drag cancelled by `pointercancel`, or released where no click follows - would stay
 * armed indefinitely and eat the user's next unrelated press, which reads as the app ignoring a click.
 */
function swallowNextClick() {
    const eat = e => {
        e.stopPropagation();
        e.preventDefault();
    };

    document.addEventListener('click', eat, { capture: true, once: true });

    // The click follows the release in the same task; anything later is not this gesture's.
    setTimeout(() => document.removeEventListener('click', eat, { capture: true }), 0);
}

/**
 * Pane seam drag. Flex values are mutated straight on the DOM for the duration of the gesture;
 * .NET hears the final numbers once, on pointerup.
 */
export function beginSplitDrag(host, index, event, dotnet) {
    if (!host) return;

    const panes = Array.from(host.querySelectorAll(':scope > .pane'));
    const seams = Array.from(host.querySelectorAll(':scope > .pane-splitter'));
    if (index < 0 || index + 1 >= panes.length) return;

    const startX = event.clientX;
    const flexes = panes.map(p => parseFloat(p.style.flexGrow) || 1);
    const total = flexes.reduce((sum, f) => sum + f, 0);

    // Seams are flex items of their own, so they are not part of the space the flex units divide up.
    const content = host.clientWidth - seams.reduce((sum, el) => sum + el.offsetWidth, 0);
    if (content <= 0 || total <= 0) return;

    const perPixel = total / content;              // flex units per pixel
    const minFlex = 220 * perPixel;

    // A pane already narrower than the minimum must not be forced wider by the clamp.
    const roomLeft = Math.max(0, flexes[index] - minFlex);
    const roomRight = Math.max(0, flexes[index + 1] - minFlex);

    const seam = seams[index];
    if (seam) seam.classList.add('dragging');

    dragSession(
        e => {
            const delta = Math.max(-roomLeft, Math.min(roomRight, (e.clientX - startX) * perPixel));
            panes[index].style.flexGrow = flexes[index] + delta;
            panes[index + 1].style.flexGrow = flexes[index + 1] - delta;
        },
        () => {
            if (seam) seam.classList.remove('dragging');
            dotnet.invokeMethodAsync('OnSplitDragged', panes.map(p => parseFloat(p.style.flexGrow) || 1));
        },
        'col-resize');
}

/**
 * Column resize. The column and the table width are mutated straight on the DOM for the duration of the
 * gesture; .NET hears the final width once, on release, and only if it actually changed - and hears it by
 * column name, because by then the grid may have reprojected and the index may name something else.
 *
 * The table's width is the sum of its columns, so growing one column grows the table and pushes the far
 * columns further out of the scroller - and shrinking one pulls them back in.
 */
export function beginColumnResize(table, index, column, startWidth, event, dotnet) {
    if (!table) return;

    const col = table.querySelectorAll('colgroup > col')[index];
    if (!col) return;

    const startX = event.clientX;
    const startTotal = parseFloat(table.style.width) || table.getBoundingClientRect().width;
    const min = 48;
    const max = 900;

    const grip = table.querySelectorAll('thead th')[index]?.querySelector('.col-grip');
    if (grip) grip.classList.add('resizing');

    let width = startWidth;

    dragSession(
        e => {
            width = Math.max(min, Math.min(max, Math.round(startWidth + (e.clientX - startX))));
            col.style.width = width + 'px';
            table.style.width = (startTotal - startWidth + width) + 'px';
        },
        () => {
            if (grip) grip.classList.remove('resizing');

            // A plain click - or the two that make up a double-click reset - must not write an override.
            // The column goes back by name: the index is only good for as long as the pointer is down.
            if (width !== startWidth)
                dotnet.invokeMethodAsync('OnColumnResized', column, width);
        },
        'col-resize');
}

/**
 * Tab gesture. Sideways is a reorder inside the strip, shown live by a drop marker; a release over the
 * rail pins instead. `toIndex` is an insertion slot in the strip as it looks now - .NET compensates for
 * the dragged tab's own removal.
 */
export function beginTabDrag(strip, viewId, canPin, event, dotnet) {
    if (!strip) return;

    const startX = event.clientX;
    const startY = event.clientY;

    const tabs = () => Array.from(strip.querySelectorAll(':scope > .tab'));
    const source = tabs().find(t => t.dataset.viewId === viewId);

    let armed = false;
    let marker = null;
    let dropIndex = -1;

    const slotAt = x => {
        const list = tabs();
        for (let i = 0; i < list.length; i++) {
            const rect = list[i].getBoundingClientRect();
            if (x < rect.left + rect.width / 2)
                return i;
        }
        return list.length;
    };

    const place = x => {
        const list = tabs();
        dropIndex = slotAt(x);

        const stripRect = strip.getBoundingClientRect();
        const anchor = list[dropIndex];
        const edge = anchor
            ? anchor.getBoundingClientRect().left
            : (list.length ? list[list.length - 1].getBoundingClientRect().right : stripRect.left);

        marker.style.left = (edge - stripRect.left + strip.scrollLeft) + 'px';
    };

    const arm = () => {
        armed = true;
        marker = document.createElement('div');
        marker.className = 'tab-drop-marker';
        strip.appendChild(marker);
        if (source) source.classList.add('dragging');
    };

    const overRail = (x, y) => {
        const rail = document.querySelector('.rail');
        return rail && pointInside(rail.getBoundingClientRect(), x, y);
    };

    dragSession(
        e => {
            if (!armed) {
                if (Math.hypot(e.clientX - startX, e.clientY - startY) <= 5)
                    return;
                arm();
            }

            const pinning = canPin && overRail(e.clientX, e.clientY);
            marker.hidden = pinning;
            document.body.style.cursor = pinning ? 'copy' : 'grabbing';

            if (!pinning)
                place(e.clientX);
        },
        (e, clicked) => {
            if (marker) marker.remove();
            if (source) source.classList.remove('dragging');
            if (!armed) return;

            if (clicked) swallowNextClick();

            if (canPin && overRail(e.clientX, e.clientY))
                dotnet.invokeMethodAsync('OnPinDropped', viewId, 'pin');
            else if (dropIndex >= 0)
                dotnet.invokeMethodAsync('OnTabReordered', viewId, dropIndex);
        },
        'grabbing');
}

/**
 * Drag a rail item onto (or a pinned item off) the rail. One callback, on drop.
 * `mode` is 'pin' or 'unpin'; the drop is accepted when the pointer is over the rail for 'pin'
 * and away from it for 'unpin'.
 */
export function beginPinDrag(key, mode, event, dotnet) {
    const startX = event.clientX;
    const startY = event.clientY;
    let armed = false;

    dragSession(
        e => {
            if (!armed && Math.hypot(e.clientX - startX, e.clientY - startY) > 5) {
                armed = true;
                document.body.style.cursor = mode === 'pin' ? 'copy' : 'no-drop';
            }
        },
        (e, clicked) => {
            if (!armed) return;

            if (clicked) swallowNextClick();

            const rail = document.querySelector('.rail');
            const over = rail && pointInside(rail.getBoundingClientRect(), e.clientX, e.clientY);

            if ((mode === 'pin' && over) || (mode === 'unpin' && !over))
                dotnet.invokeMethodAsync('OnPinDropped', key, mode);
        },
        '');
}

function pointInside(rect, x, y) {
    return x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom;
}
