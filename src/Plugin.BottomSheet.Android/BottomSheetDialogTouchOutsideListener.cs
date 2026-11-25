using Android.Views.Inspector;
using AndroidX.AppCompat.App;

namespace Plugin.BottomSheet.Android;

/// <summary>
/// Provides a mechanism to handle touch events occurring outside a bottom sheet,
/// ensuring proper interaction and event propagation in non-modal scenarios.
/// </summary>
/// <remarks>
/// The dialog window fills the screen and sits above the activity window and the activity's sub-windows
/// (toolbar overflow menus, popup menus, spinner drop-downs). Forwarding only to the activity leaves those
/// popups unreachable, so a touch that lands on one of them goes to that popup instead.
/// </remarks>
internal sealed class BottomSheetDialogTouchOutsideListener : Java.Lang.Object, View.IOnTouchListener
{
    private const int FirstSubWindowType = 1000;
    private const int LastSubWindowType = 1999;

    private readonly AppCompatActivity _activity;
    private readonly global::Android.App.Dialog _dialog;

    private View? _gestureTarget;

    /// <summary>
    /// Initializes a new instance of the <see cref="BottomSheetDialogTouchOutsideListener"/> class.
    /// Handles touch events outside the bottom sheet for non-modal behavior.
    /// </summary>
    /// <param name="activity">The activity hosting the bottom sheet dialog.</param>
    /// <param name="dialog">The bottom sheet dialog whose window receives the touches.</param>
    public BottomSheetDialogTouchOutsideListener(AppCompatActivity activity, global::Android.App.Dialog dialog)
    {
        _activity = activity;
        _dialog = dialog;
    }

    /// <summary>
    /// Handles touch events and dispatches them to the popup under the touch, or to the activity.
    /// </summary>
    /// <param name="v">The view that was touched.</param>
    /// <param name="e">The motion event details.</param>
    /// <returns>Always returns false to allow further event processing.</returns>
    public bool OnTouch(View? v, MotionEvent? e)
    {
        if (e is null)
        {
            return false;
        }

        // Pick the target on DOWN and keep it for the rest of the gesture.
        if (e.ActionMasked == MotionEventActions.Down)
        {
            _gestureTarget = FindSubWindowAt(e.RawX, e.RawY);
        }

        if (_gestureTarget is not null
            && _gestureTarget.IsAttachedToWindow)
        {
            DispatchToWindow(_gestureTarget, e);
        }
        else
        {
            _ = _activity.DispatchTouchEvent(e);
        }

        if (e.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel)
        {
            _gestureTarget = null;
        }

        return false;
    }

    /// <summary>
    /// Dispatches a copy of the event to the root view of another window, translated into that window's coordinates.
    /// </summary>
    /// <param name="windowRoot">Root view of the target window.</param>
    /// <param name="e">The motion event received by the dialog window.</param>
    private static void DispatchToWindow(View windowRoot, MotionEvent e)
    {
        int[] location = new int[2];
        windowRoot.GetLocationOnScreen(location);

        MotionEvent copy = MotionEvent.Obtain(e);
        copy.OffsetLocation(e.RawX - e.GetX() - location[0], e.RawY - e.GetY() - location[1]);

        _ = windowRoot.DispatchTouchEvent(copy);

        copy.Recycle();
    }

    /// <summary>
    /// Finds the topmost visible sub-window (for example a popup menu) of this app that contains the given screen point.
    /// </summary>
    /// <param name="rawX">Screen x coordinate.</param>
    /// <param name="rawY">Screen y coordinate.</param>
    /// <returns>The root view of the sub-window, or null if there is none at that point.</returns>
    private View? FindSubWindowAt(float rawX, float rawY)
    {
        // WindowInspector (API 29+) is the only public way to list the app's windows.
        if (OperatingSystem.IsAndroidVersionAtLeast(29) == false)
        {
            return null;
        }

        IList<View> windowRoots = WindowInspector.GlobalWindowViews;
        View? dialogRoot = _dialog.Window?.DecorView;
        int[] location = new int[2];

        // Windows are listed in the order they were added, so walk backwards to find the topmost one.
        for (int i = windowRoots.Count - 1; i >= 0; i--)
        {
            View root = windowRoots[i];

            if (root == dialogRoot
                || root.IsShown == false
                || root.LayoutParameters is not WindowManagerLayoutParams layoutParams
                || (int)layoutParams.Type < FirstSubWindowType
                || (int)layoutParams.Type > LastSubWindowType)
            {
                continue;
            }

            root.GetLocationOnScreen(location);

            if (rawX >= location[0]
                && rawX < location[0] + root.Width
                && rawY >= location[1]
                && rawY < location[1] + root.Height)
            {
                return root;
            }
        }

        return null;
    }
}
