namespace TVSoundController;

/// <summary>
/// The class to keep a range bounds with hysteresis.
/// It is used to tracker the events when a signal crosses the boundaries.
/// Outer boundaries (<see cref="Min"/> and <see cref="Max"/>) are used to detect signal-out events.
/// Inner boundaries (higher than <see cref="Min"/> by <see cref="MinHist"/> and 
/// lower than <see cref="Max"/> by <see cref="MaxHist"/>) are used  to setect signal-in events.
/// </summary>
public class RangeThresholdDetector
{
    /// <summary>
    /// Direction of the signal crossing a boundary
    /// </summary>
    public enum Cross
    {
        None,
        ExitUp,
        ExitDown,
        Enter
    }

    /// <summary>
    /// Lower outer boundary
    /// </summary>
    public double Min
    {
        get => _lowOuter;
        set
        {
            CheckProperties(value, _highOuter, value + MinHist, _highInner);
            var hist = MinHist;
            _lowOuter = value;
            _lowInner = _lowOuter + hist;
        }
    }

    /// <summary>
    /// Higher outer boundary
    /// </summary>
    public double Max
    {
        get => _highOuter;
        set
        {
            CheckProperties(_lowOuter, value, _lowInner, value - MaxHist);
            var hist = MaxHist;
            _highOuter = value;
            _highInner = _highOuter - hist;
        }
    }

    /// <summary>
    /// Hysteresis gap used to calculate Lower inner boundary
    /// </summary>
    public double MinHist
    {
        get => _lowInner - _lowOuter;
        set
        {
            CheckProperties(_lowOuter, _highOuter, _lowOuter + value, _highInner);
            _lowInner = _lowOuter + value;
        }
    }

    /// <summary>
    /// Hysteresis gap used to calculate Higher inner boundary
    /// </summary>
    public double MaxHist
    {
        get => _highOuter - _highInner;
        set
        {
            CheckProperties(_lowOuter, _highOuter, _lowInner, _highOuter - value);
            _highInner = _highOuter - value;
        }
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="lowOuter">Lower outer boundary</param>
    /// <param name="highOuter">Higher outer boundary</param>
    /// <param name="lowHist">Hysteresis gap used to calculate Lower inner boundary</param>
    /// <param name="highHist"> Hysteresis gap used to calculate Higher inner boundary</param>
    public RangeThresholdDetector(double lowOuter, double highOuter, double lowHist = 0, double highHist = 0)
    {
        CheckProperties(lowOuter, highOuter, lowOuter + lowHist, highOuter - highHist);

        _lowOuter = lowOuter;
        _highOuter = highOuter;

        _lowInner = _lowOuter + lowHist;
        _highInner = _highOuter - highHist;
    }

    /// <summary>
    /// Used to check the validaty of boundary values. Valid boundaries has lower out boundary smaller
    /// than lower inner boundary, that in turn smaller than higher inner boundary, that in turn
    /// smaller than the higher outer boundary.
    /// </summary>
    /// <param name="lowOuter">Lower outer boundary</param>
    /// <param name="highOuter">Higher outer boundary</param>
    /// <param name="lowInner">Lower inner boundary</param>
    /// <param name="highInner">Higher inner boundary</param>
    /// <exception cref="System.ArgumentException">Trows if the boundary configuration is invalid</exception>
    private static void CheckProperties(double lowOuter, double highOuter, double lowInner, double highInner)
    {
        if (lowOuter > lowInner ||
            lowInner >= highInner ||
            highInner > highOuter)
            throw new System.ArgumentException("One of the input values are invalid");
    }

    /// <summary>
    /// Examines the value and return the boundary crossing event
    /// </summary>
    /// <param name="value">Value to examine</param>
    /// <returns>Crossing event, if any (i.e. <see cref="Cross.None"/>)</returns>
    public Cross Feed(double value)
    {
        Cross result = Cross.None;

        if (_position != Position.Above && value > Max)
        {
            result = Cross.ExitUp;
            _position = Position.Above;
        }
        else if (_position == Position.Above && value < _highInner)
        {
            result = value < Min ? Cross.ExitDown : Cross.Enter;
            _position = value < Min ? Position.Below : Position.Inside;
        }
        else if (_position != Position.Below && value < Min)
        {
            result = Cross.ExitDown;
            _position = Position.Below;
        }
        else if (_position == Position.Below && value > _lowInner)
        {
            result = value > Max ? Cross.ExitUp : Cross.Enter;
            _position = value > Max ? Position.Above : Position.Inside;
        }

        return result;
    }

    // Internal

    enum Position
    {
        Inside,
        Below,
        Above
    }

    double _lowOuter;
    double _highOuter;
    double _lowInner;
    double _highInner;

    Position _position = Position.Inside;
}