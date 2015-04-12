using System;
using System.Collections.Generic;

//taken from https://gist.github.com/jorgenpt/9f610a8ddf693775cd8c

namespace JPT
{
    public class TextProgressBar : IDisposable
    {
        private long _progress;
        private long _total;
        private int _width;
        private int _lastFilledSlots;
        private int _cursorRow;

        public TextProgressBar(long total, int width = -1, bool drawImmediately = true)
        {
            _progress = 0;
            _total = total;

            if (width < 0)
                _width = Console.WindowWidth - string.Format("[]   {0} of {0}", _total).Length;
            else
                _width = width;

            _lastFilledSlots = -1;
            _cursorRow = -1;

            if (drawImmediately)
                Update(0);
        }

        public void Update(long progress)
        {
            _progress = Math.Max(Math.Min(progress, _total), 0);

            if (_cursorRow < 0)
            {
                _cursorRow = Console.CursorTop;
                Console.CursorTop++;
                Console.CursorLeft = 0;
            }

            int filledSlots = (int)Math.Floor(_width * ((double)progress) / _total);
            if (filledSlots != _lastFilledSlots)
            {
                _lastFilledSlots = filledSlots;
                DrawBar();
            }

            DrawText();

            if (Console.CursorTop == _cursorRow)
                Console.CursorLeft = Console.WindowWidth - 1;
        }

        public void ForceDraw()
        {
            DrawBar();
            DrawText();

            if (Console.CursorTop == _cursorRow)
                Console.CursorLeft = Console.WindowWidth - 1;
        }

        public static TextProgressBar operator ++(TextProgressBar bar)
        {
            bar.Increment();
            return bar;
        }

        public void Increment()
        {
            Update(_progress + 1);
        }

        public void Dispose()
        {
            Update(_total);

            if (Console.CursorTop == _cursorRow)
                Console.WriteLine("");
        }

        private void DrawBar()
        {
            using (new ConsoleStateSaver())
            {
                Console.CursorVisible = false;
                Console.CursorTop = _cursorRow;

                // Draw the outline of the progress bar
                Console.CursorLeft = _width + 1;
                Console.Write("]");

                Console.CursorLeft = 0;
                Console.Write("[");
                // Draw progressed part
                Console.BackgroundColor = ConsoleColor.Green;
                Console.Write(new String(' ', _lastFilledSlots));

                // Draw remaining part
                Console.BackgroundColor = ConsoleColor.Black;
                Console.Write(new String(' ', _width - _lastFilledSlots));
            }
        }

        private void DrawText()
        {
            using (new ConsoleStateSaver())
            {
                // Write progress text
                Console.CursorVisible = false;
                Console.CursorTop = _cursorRow;
                Console.CursorLeft = _width + 4;
                Console.Write("{0} of {1}", _progress.ToString().PadLeft(_total.ToString().Length), _total);
                Console.Write(new String(' ', Console.WindowWidth - Console.CursorLeft));
            }
        }

        private class ConsoleStateSaver : IDisposable
        {
            ConsoleColor _bgColor;
            int _cursorTop, _cursorLeft;
            bool _cursorVisible;

            public ConsoleStateSaver()
            {
                _bgColor = Console.BackgroundColor;
                _cursorTop = Console.CursorTop;
                _cursorLeft = Console.CursorLeft;
                _cursorVisible = Console.CursorVisible;
            }

            public void Dispose()
            {
                RestoreState();
            }

            public void RestoreState()
            {
                Console.BackgroundColor = _bgColor;
                Console.CursorTop = _cursorTop;
                Console.CursorLeft = _cursorLeft;
                Console.CursorVisible = _cursorVisible;
            }
        }
    }
}
