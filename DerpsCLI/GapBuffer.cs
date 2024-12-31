using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerraFX.Interop.Windows;

namespace DerpsCLI
{
    // The GapWindow keeps track of the gap in the buffer (from and to positions).
    public record GapWindow(int from, int to);

    public class GapBuffer
    {
        // Constant for the gap size
        private const int GAP_SIZE = 64;

        // Initial gap window with size GAP_SIZE
        private GapWindow _gapWindow = new(0, GAP_SIZE);

        // The actual text buffer stored in a List<char> for easy manipulation
        private List<char> _textBuffer = new List<char>();

        // Lock object to avoid race conditions (if using multi-threading)
        private static readonly object queueLock = new object();

        private void MoveFromPtrTo(int position)
        {
            while (position < _gapWindow.from)
            {
                _gapWindow = new(_gapWindow.from - 1, _gapWindow.to - 1);
                _textBuffer[_gapWindow.to + 1] = _textBuffer[_gapWindow.from];
                _textBuffer[_gapWindow.from] = '\0'; // NULL character
            }
        }

        private void MoveToPtrTo(int position)
        {
            while (position > _gapWindow.from)
            {
                _gapWindow = new(_gapWindow.from + 1, _gapWindow.to + 1);
                _textBuffer[_gapWindow.from - 1] = _textBuffer[_gapWindow.to];
                _textBuffer[_gapWindow.to] = '\0'; // NULL character
            }
        }

        private void MoveCursor(int position)
        {
            if (position < _gapWindow.from)
            {
                MoveFromPtrTo(position);
            }
            else
            {
                MoveToPtrTo(position);
            }
        }

        private void Grow(int size, int position)
        {
            // Insert 'numberOfNulls' '\0' (null character) at the specified index
            _textBuffer.InsertRange(position, Enumerable.Repeat('\0', size));
        }

        public int GetCursorPosition()
        {
            return _gapWindow.from;
        }

        public string GetFullText()
        {
            string result = "";
            int i = 0;
            while (i < _textBuffer.Count)
            {
                if (i == _gapWindow.from)
                {
                    i = _gapWindow.to + 1;
                }
                result += _textBuffer[i];
                i++;
            }

            return result;
        }

        public void Insert(String input, int position)
        {
            // If not at correct postion, move the cursor to it
            if (position != _gapWindow.from)
            {
                MoveCursor(position);
            }

            int len = input.Length;
            int i = 0;
            while (i < len)
            {
                if (_gapWindow.from == _gapWindow.to)
                {
                    // Gap closed so grow the gap
                    Grow(GAP_SIZE, _gapWindow.from);
                }

                // Insert the character in the gap and move the gap
                if (_gapWindow.from >=  _textBuffer.Count)
                {
                    _textBuffer.Add(input[i]);
                }
                else
                {
                    _textBuffer[_gapWindow.from] = input[i];
                }

                _gapWindow = new GapWindow(_gapWindow.from+1, _gapWindow.to);
                i++;
                position++;
            }
        }
    }
}
