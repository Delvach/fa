using UnityEngine;

internal sealed class FAUiInputCore
{
    internal enum StickPreference
    {
        Either = 0,
        Right = 1,
        Left = 2,
        Strongest = 3
    }

    internal enum AxisLock
    {
        None = 0,
        Horizontal = 1,
        Vertical = 2
    }

    internal struct StickRead
    {
        public string slot;
        public Vector2 navigation;
        public string source;
        public bool valid;
        public bool active;
    }

    private const float NavigationDeadzone = 0.32f;
    private const float NavigationAxisReleaseDeadzone = 0.18f;
    private const float NavigationSourceActiveThreshold = 0.02f;
    private const float NavigationStickContinuitySeconds = 0.30f;

    private string lastActiveStick = "none";
    private float lastActiveStickAt = -1000f;
    private AxisLock axisLock = AxisLock.None;

    internal AxisLock CurrentAxisLock
    {
        get { return axisLock; }
    }

    internal StickRead ReadNavigation(StickPreference preference)
    {
        StickRead leftRead = ReadStick(
            "left",
            JoystickControl.Axis.LeftStickX,
            JoystickControl.Axis.LeftStickY);
        StickRead rightRead = ReadStick(
            "right",
            JoystickControl.Axis.RightStickX,
            JoystickControl.Axis.RightStickY);

        StickRead selectedRead = SelectStick(preference, leftRead, rightRead);
        selectedRead.navigation = ApplyAxisLock(selectedRead.navigation);
        selectedRead.active = IsNavigationActive(selectedRead.navigation);
        return selectedRead;
    }

    internal void ResetAxisLock()
    {
        axisLock = AxisLock.None;
    }

    private StickRead ReadStick(
        string slot,
        JoystickControl.Axis joystickXAxis,
        JoystickControl.Axis joystickYAxis)
    {
        StickRead read = new StickRead();
        read.slot = string.IsNullOrEmpty(slot) ? "none" : slot;
        read.navigation = Vector2.zero;
        read.source = "none";
        read.valid = false;
        read.active = false;

        Vector2 joystickNavigation = Vector2.zero;
        bool joystickValid = false;

        try
        {
            float joystickX;
            float joystickY;
            bool joystickXValid = TryReadJoystickAxis(joystickXAxis, out joystickX);
            bool joystickYValid = TryReadJoystickAxis(joystickYAxis, out joystickY);
            if (joystickXValid || joystickYValid)
            {
                joystickNavigation = new Vector2(joystickXValid ? joystickX : 0f, joystickYValid ? joystickY : 0f);
                joystickValid = true;
            }
        }
        catch
        {
            joystickNavigation = Vector2.zero;
            joystickValid = false;
        }

        bool joystickActive = IsNavigationActive(joystickNavigation);
        read.valid = joystickValid;

        if (joystickActive)
        {
            read.navigation = joystickNavigation;
            read.source = "joystick";
        }
        else if (joystickValid)
        {
            read.navigation = joystickNavigation;
            read.source = "joystick_idle";
        }

        read.active = IsNavigationActive(read.navigation);
        return read;
    }

    private StickRead SelectStick(StickPreference preference, StickRead leftRead, StickRead rightRead)
    {
        switch (preference)
        {
            case StickPreference.Either:
                return SelectEitherStick(leftRead, rightRead);
            case StickPreference.Left:
                return leftRead;
            case StickPreference.Strongest:
                return SelectStrongestStick(leftRead, rightRead);
            case StickPreference.Right:
            default:
                return rightRead;
        }
    }

    private StickRead SelectEitherStick(StickRead leftRead, StickRead rightRead)
    {
        if (leftRead.active && rightRead.active)
            return SelectStrongestStick(leftRead, rightRead);

        if (leftRead.active)
            return leftRead;

        if (rightRead.active)
            return rightRead;

        return SelectStrongestStick(leftRead, rightRead);
    }

    private StickRead SelectStrongestStick(StickRead leftRead, StickRead rightRead)
    {
        StickRead selectedRead = new StickRead();
        selectedRead.slot = "none";
        selectedRead.navigation = Vector2.zero;
        selectedRead.source = "none";
        selectedRead.valid = false;
        selectedRead.active = false;

        if (leftRead.active && rightRead.active)
        {
            if (leftRead.navigation.sqrMagnitude > rightRead.navigation.sqrMagnitude)
                selectedRead = leftRead;
            else if (rightRead.navigation.sqrMagnitude > leftRead.navigation.sqrMagnitude)
                selectedRead = rightRead;
            else if (string.Equals(lastActiveStick, "left", System.StringComparison.Ordinal))
                selectedRead = leftRead;
            else
                selectedRead = rightRead;
        }
        else if (leftRead.active)
        {
            selectedRead = leftRead;
        }
        else if (rightRead.active)
        {
            selectedRead = rightRead;
        }
        else
        {
            float now = Time.unscaledTime;
            if ((now - lastActiveStickAt) <= NavigationStickContinuitySeconds)
            {
                if (string.Equals(lastActiveStick, "left", System.StringComparison.Ordinal) && leftRead.valid)
                    selectedRead = leftRead;
                else if (string.Equals(lastActiveStick, "right", System.StringComparison.Ordinal) && rightRead.valid)
                    selectedRead = rightRead;
            }

            if (!selectedRead.valid)
            {
                if (rightRead.valid)
                    selectedRead = rightRead;
                else if (leftRead.valid)
                    selectedRead = leftRead;
            }
        }

        if (selectedRead.active && !string.IsNullOrEmpty(selectedRead.slot) && !string.Equals(selectedRead.slot, "none", System.StringComparison.Ordinal))
        {
            lastActiveStick = selectedRead.slot;
            lastActiveStickAt = Time.unscaledTime;
        }

        return selectedRead;
    }

    private static bool TryReadJoystickAxis(JoystickControl.Axis axis, out float value)
    {
        value = 0f;
        try
        {
            value = JoystickControl.GetAxis(axis);
            return true;
        }
        catch
        {
            value = 0f;
            return false;
        }
    }

    private static bool IsNavigationActive(Vector2 navigation)
    {
        return Mathf.Abs(navigation.x) >= NavigationSourceActiveThreshold
            || Mathf.Abs(navigation.y) >= NavigationSourceActiveThreshold;
    }

    private Vector2 ApplyAxisLock(Vector2 navigation)
    {
        float absX = Mathf.Abs(navigation.x);
        float absY = Mathf.Abs(navigation.y);
        bool xActive = absX >= NavigationDeadzone;
        bool yActive = absY >= NavigationDeadzone;

        if (absX <= NavigationAxisReleaseDeadzone
            && absY <= NavigationAxisReleaseDeadzone)
        {
            axisLock = AxisLock.None;
            return Vector2.zero;
        }

        if (axisLock == AxisLock.Horizontal)
        {
            if (absX <= NavigationAxisReleaseDeadzone)
            {
                axisLock = AxisLock.None;
                return Vector2.zero;
            }

            return new Vector2(navigation.x, 0f);
        }

        if (axisLock == AxisLock.Vertical)
        {
            if (absY <= NavigationAxisReleaseDeadzone)
            {
                axisLock = AxisLock.None;
                return Vector2.zero;
            }

            return new Vector2(0f, navigation.y);
        }

        if (xActive && (!yActive || absX >= absY))
            axisLock = AxisLock.Horizontal;
        else if (yActive)
            axisLock = AxisLock.Vertical;

        if (axisLock == AxisLock.Horizontal)
            return new Vector2(navigation.x, 0f);

        if (axisLock == AxisLock.Vertical)
            return new Vector2(0f, navigation.y);

        return Vector2.zero;
    }
}
