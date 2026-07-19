using System.Collections.Generic;

namespace RadioControlMod
{
    internal sealed class RadioProgram
    {
        private readonly List<RadioStep> _steps;
        private readonly string _source;
        private int _index;
        private int _remainingFrames;
        private int _releaseFrames;

        public RadioProgram(List<RadioStep> steps, string source)
        {
            _steps = steps;
            _source = source;
            _index = 0;
            _remainingFrames = steps.Count > 0 ? steps[0].Frames : 0;
        }

        public string Source
        {
            get { return _source; }
        }

        public int StepIndex
        {
            get { return _index + 1; }
        }

        public int StepCount
        {
            get { return _steps.Count; }
        }

        public int RemainingFrames
        {
            get { return _remainingFrames; }
        }

        public bool IsComplete
        {
            get { return _index >= _steps.Count; }
        }

        public bool IsReleasing
        {
            get { return !IsComplete && _releaseFrames > 0; }
        }

        public RadioStep ActiveStep
        {
            get
            {
                if (IsComplete || IsReleasing)
                {
                    return null;
                }

                return _steps[_index];
            }
        }

        public string Status
        {
            get
            {
                if (IsComplete)
                {
                    return "Done";
                }

                if (IsReleasing)
                {
                    return "release " + _releaseFrames + "f";
                }

                return _steps[_index].Name + " " + _remainingFrames + "f";
            }
        }

        public void AdvanceOneFrame()
        {
            if (IsComplete)
            {
                return;
            }

            if (IsReleasing)
            {
                _releaseFrames--;

                if (_releaseFrames > 0)
                {
                    return;
                }

                _index++;

                if (!IsComplete)
                {
                    _remainingFrames = _steps[_index].Frames;
                }

                return;
            }

            _remainingFrames--;

            if (_remainingFrames > 0)
            {
                return;
            }

            _releaseFrames = 1;
        }
    }
}
