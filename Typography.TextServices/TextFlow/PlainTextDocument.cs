//MIT, 2019-present, WinterDev
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

using LayoutFarm.TextEditing.Commands;
using LayoutFarm.TextEditing;
using System.Collections;

namespace Typography.Text
{
    public enum PlainTextLineEnd : byte
    {
        None,
        /// <summary>
        /// \r\n
        /// </summary>
        CRLF,
        /// <summary>
        /// \n
        /// </summary>
        LF,
    }


    /// <summary>
    /// immutable plain text line
    /// </summary>
    public class PlainTextLine
    {
        public PlainTextLine() { }
        public CharBufferSegment Content { get; set; }

        public PlainTextLineEnd EndWith { get; set; }

        public string GetText() => PlainTextUtils.ReadString(Content);
        public void WriteTo(StringBuilder sb)
        {
            PlainTextUtils.WriteTo(Content, sb);
        }
        public int CharCount => Content.len;

        public PlainTextLine Clone() => new PlainTextLine() { Content = Content };
        //

#if DEBUG
        public override string ToString()
        {
            return GetText();
        }
#endif
    }

    static class PlainTextUtils
    {
        [ThreadStatic]
        static StringBuilder s_sb;
        [ThreadStatic]
        static TextCopyBufferUtf32 s_copyBuffer;

        static void Init()
        {
            s_sb = new StringBuilder();
            s_copyBuffer = new TextCopyBufferUtf32();
        }
        public static string ReadString(CharBufferSegment charSegment)
        {
            if (s_sb != null) { Init(); }

            charSegment.WriteTo(s_copyBuffer);
            return ReadString(s_copyBuffer);
        }
        public static string ReadString(TextCopyBufferUtf32 copyBuffer)
        {
            if (s_sb != null) { Init(); }

            s_sb.Length = 0;//reset  
            copyBuffer.CopyTo(s_sb);
            return s_sb.ToString();
        }
        public static string ReadCurrentLine(PlainTextEditSession block)
        {
            if (s_sb != null) { Init(); }

            s_sb.Length = 0;//reset
            s_copyBuffer.Clear();//reset
            block.ReadCurrentLine(s_copyBuffer);
            s_copyBuffer.CopyTo(s_sb);
            return s_sb.ToString();
        }

        public static void WriteTo(CharBufferSegment charSegment, StringBuilder sb)
        {
            if (s_sb != null) { Init(); }

            // 
            s_copyBuffer.Clear();//reset
            charSegment.WriteTo(s_copyBuffer);
            s_copyBuffer.CopyTo(sb);
        }
    }




    class LineEditor
    {
        PlainTextEditSession _textBlock;//owner
        PlainTextLine _line;

        //TODO: review a proper data structure again,use tree of character? (eg RB tree?)
        //esp for a long line insertion, deletion
        readonly TextBuffer<int> _arrList = new TextBuffer<int>();

        bool _changed;
        bool _loaded;
        int _initContentLen;

        /// <summary>
        /// begin at 0 for new char index
        /// </summary>
        int _newCharIndex;//new character index 1:1 based on backend buffer (utf16 or utf32)
        internal LineEditor()
        {

        }
        internal void Bind(PlainTextEditSession textBlock)
        {
            _textBlock = textBlock;
        }
        public void Read(TextCopyBufferUtf32 output)
        {
            //read data from this line and write to output
            if (_loaded)
            {
                output.AppendData(_arrList.UnsafeInternalArray, 0, _arrList.Length);
            }
            else
            {
                //not load then
                _line.Content.WriteTo(output);
            }

        }
        public void Read(TextCopyBufferUtf32 output, int index, int len)
        {
            if (_loaded)
            {
                output.AppendData(_arrList.UnsafeInternalArray, index, len);
            }
            else
            {
                _line.Content.WriteTo(output, index, len);
            }
        }
        public void Bind(PlainTextLine line)
        {
            if (_line == line)
            {
                return;//same line
            }
            //
            if (_line != null && _changed)
            {
                //update content back             
                _line.Content = _textBlock.CharSource.NewSpan(_arrList.UnsafeInternalArray, 0, _arrList.Length);
            }
            //
            _line = line;
            //reset
            _newCharIndex = 0;
            _initContentLen = line.Content.len;
            _changed = false;
            _loaded = false;
            _arrList.Clear();

            NewCharIndex = 0;
        }


        /// <summary>
        /// load content of each line to edit mode
        /// </summary>
        void Load()
        {
            if (_loaded) { return; }

            //
            CharBufferSegment content = _line.Content;
            if (content.len > 0)
            {
                //copy content to temp buffer
                content.WriteTo(_arrList);
            }
            _loaded = true;

        }
        /// <summary>
        /// character index for new insertion
        /// </summary>
        public int NewCharIndex
        {
            get => _newCharIndex;
            set
            {
                if (value <= ((!_loaded) ? _initContentLen : _arrList.Count))
                {
                    _newCharIndex = value;
                }
                else
                {
                    //not accept
                    //warn, and set default
                }
            }
        }

        public bool CharIndexOnTheEnd => _newCharIndex == Count;

        public void AddText(TextCopyBufferUtf32 copyBuffer, int start, int len)
        {
            if (!_loaded) { Load(); }//

            copyBuffer.CopyTo(_arrList, _newCharIndex, start, len);
            _newCharIndex += len;
            _changed = true;
        }

        public void Split(TextCopyBufferUtf32 rightPart)
        {
            if (!_loaded) { Load(); }//

            int rightPartLen = _arrList.Count - _newCharIndex;
            rightPart.AppendData(_arrList, _newCharIndex, rightPartLen);
            _arrList.Remove(_newCharIndex, rightPartLen);
            _changed = true;
        }
        /// <summary>
        /// single delete
        /// </summary>
        public void DoDelete()
        {
            if (!_loaded) { Load(); }

            if (_newCharIndex < Count)
            {
                _newCharIndex++;
                DoBackSpace();
                _changed = true;
            }
        }
        public void DeleteRange(int start, int len)
        {
            if (!_loaded) { Load(); }

            _arrList.Remove(start, len);
            _newCharIndex = start;
            _changed = true;
        }
        /// <summary>
        /// single backspace
        /// </summary>
        public void DoBackSpace()
        {
            if (!_loaded) { Load(); }

            if (_newCharIndex < 1) { return; }//early exit
            //
            _arrList.Remove(_newCharIndex - 1, 1);
            _newCharIndex--;
            _changed = true;
        }
        /// <summary>
        /// add character to current index
        /// </summary>
        /// <param name="c"></param>
        public void AddChar(int c)
        {

            if (!_loaded) { Load(); }

            if (_arrList.Count == _newCharIndex)
            {
                //the last one
                _arrList.Append(c);
            }
            else
            {
                _arrList.Insert(_newCharIndex, c);
            }
            _newCharIndex++;
            _changed = true;
        }
        public void Clear()
        {
            if (!_loaded) { Load(); }//

            _arrList.Clear();
            _newCharIndex = 0;
            _changed = true;
        }
        /// <summary>
        /// character count
        /// </summary>
        public int Count => _loaded ? _arrList.Count : _initContentLen;
    }

    public class PlainTextDocument1 : IEnumerable<PlainTextLine>
    {
        CharSource _charSource = new CharSource();
        readonly List<PlainTextLine> _lines = new List<PlainTextLine>();
        public PlainTextDocument1()
        {

        }
        public int LineCount => _lines.Count;
        internal List<PlainTextLine> UnsafeInternalList => _lines;
        internal CharSource CharSource => _charSource;
        public void LoadText(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                _lines.Add(new PlainTextLine() { Content = _charSource.NewSegment(new TextBufferSpan(line.ToCharArray())) });
            }
        }
        public void LoadText(string text)
        {
            using (System.IO.StringReader reader = new System.IO.StringReader(text))
            {
                string line = reader.ReadLine();
                while (line != null)
                {
                    //...       

                    _lines.Add(new PlainTextLine() { Content = _charSource.NewSegment(new TextBufferSpan(line.ToCharArray())) });
                    line = reader.ReadLine();
                }
            }
        }

        public IEnumerator<PlainTextLine> GetEnumerator()
        {
            return ((IEnumerable<PlainTextLine>)_lines).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_lines).GetEnumerator();
        }
    }

    public class PlainTextEditSession : ITextFlowEditSession
    {
        int _currentLineNo;
        PlainTextLine _currentLine;
        LineEditor _lineEditor = new LineEditor();
        TextCopyBufferUtf32 _copyBuffer = new TextCopyBufferUtf32();
        List<PlainTextLine> _lines;
        PlainTextDocument1 _doc;
        public PlainTextEditSession()
        {
        }
        public void LoadPlainText(PlainTextDocument1 doc)
        {
            _doc = doc;
            _lines = doc.UnsafeInternalList;
            //move to 
            if (_lines.Count == 0)
            {
                _lines.Add(new PlainTextLine());
                CurrentLineNumber = 0;
            }
            _lineEditor = new LineEditor();
            _lineEditor.Bind(this);
            CurrentLineNumber = 0;
            NewCharIndex = 0;
        }
        public void Clear()
        {
            _lines.Clear();
            _lines.Add(new PlainTextLine());
            CurrentLineNumber = 0;
        }

        internal CharSource CharSource => _doc.CharSource;
        /// <summary>
        /// current line new character index
        /// </summary>
        public int NewCharIndex
        {
            get => _lineEditor.NewCharIndex;
            set => _lineEditor.NewCharIndex = value;
        }

        public void ReadCurrentLine(TextCopyBufferUtf32 output)
        {
            //read from current line editor
            _lineEditor.Read(output);
        }
        public int LineCount => _lines.Count;
        public int CurrentLineNumber
        {
            get => _currentLineNo;
            set
            {
                if (value >= 0 && value < _lines.Count)
                {
                    _currentLineNo = value;

                    PlainTextLine line = _lines[value];
                    if (_currentLine != line)
                    {
                        //change 
                        _currentLine = line;
                        _lineEditor.Bind(line);
                    }
                }
            }
        }

        public void AddText(string s)
        {
            if (!_selection.isEmpty)
            {
                DeleteSelection();
            }

            //to utf32
            if (_currentLine != null)
            {
                //1.  
                //parse for each line
                _copyBuffer.Clear();
                _copyBuffer.AppendData(s);
                AddText(_copyBuffer);
            }
            else
            {
                //TODO: review this
                throw new NotSupportedException();
            }
        }

        public void AddChar(int c)
        {
            if (!_selection.isEmpty)
            {
                DeleteSelection();
            }

            if (_currentLine != null)
            {
                //1.  
                _lineEditor.AddChar(c);
            }
            else
            {
                //TODO: review this
                throw new NotSupportedException();
            }
        }

        public void AddText(TextCopyBuffer copy)
        {
            if (!_selection.isEmpty)
            {
                DeleteSelection();
            }

            _copyBuffer.GetReader(out TextBreak.InputReader reader);

            while (reader.Readline(out int begin, out int len, out TextBreak.InputReader.LineEnd lineEnd))
            {
                _lineEditor.AddText(_copyBuffer, begin, len);

                if (lineEnd != TextBreak.InputReader.LineEnd.None)
                {
                    //insert new line
                    if (_currentLineNo == _lines.Count - 1)
                    {
                        //now we are in the last line
                        _lines.Add(new PlainTextLine());
                        CurrentLineNumber++;
                    }
                    else
                    {
                        _lines.Insert(CurrentLineNumber + 1, new PlainTextLine());
                        CurrentLineNumber++;//move to lower
                    }
                }
            }
        }

        public bool DoBackspace()
        {
            if (!_selection.isEmpty)
            {
                return DeleteSelection();
            }

            //do single backspace
            if (_lineEditor.NewCharIndex == 0)
            {
                //begin of the current line
                if (CurrentLineNumber == 0)
                {
                    return false;
                }
                //move up
                //and join content on this 

                int charCount = _lineEditor.Count;
                if (charCount > 0)
                {
                    //copy content of current line
                    _copyBuffer.Clear();
                    _lineEditor.Read(_copyBuffer);
                }

                CurrentLineNumber--;
                //move newchar index to end line                
                _lineEditor.NewCharIndex = _lineEditor.Count;
                if (charCount > 0)
                {
                    //paste content from the lower line
                    int pos = _lineEditor.NewCharIndex;
                    _lineEditor.AddText(_copyBuffer, 0, _copyBuffer.Length);
                    _lineEditor.NewCharIndex = pos;//move to latest pos
                }
                //and delete the lower line
                _lines.RemoveAt(CurrentLineNumber + 1);
                return true;
            }
            else
            {
                //on current line
                _lineEditor.DoBackSpace();
                return true;
            }
        }

        bool DeleteSelection()
        {
            if (_selection.isEmpty) { return false; }

            //
            _selection.Normalize();
            if (_selection.startLineNo == _selection.endLineNo)
            {
                //on the sameline
                CurrentLineNumber = _selection.startLineNo;
                _lineEditor.DeleteRange(_selection.startLineNewCharIndex, _selection.endLineNewCharIndex - _selection.startLineNewCharIndex);
                _selection.Finish();
            }
            else
            {
                //more than 1 line
                CurrentLineNumber = _selection.startLineNo;
                _lineEditor.DeleteRange(_selection.startLineNewCharIndex, _lineEditor.Count - _selection.startLineNewCharIndex);
                //
                int endLineNo = _selection.endLineNo;

                int betweenCount = _selection.endLineNo - _selection.startLineNo - 1;
                if (betweenCount > 0)
                {
                    int nextLineNo = _selection.startLineNo + 1;
                    for (int i = _selection.endLineNo - 1; i >= nextLineNo; --i)
                    {
                        _lines.RemoveAt(i);
                    }
                    endLineNo -= betweenCount;
                }

                CurrentLineNumber = endLineNo;
                _lineEditor.DeleteRange(0, _selection.endLineNewCharIndex);
                _selection.Finish();

                DoBackspace();
            }

            return true;
        }
        public VisualSelectionRangeSnapShot DoDelete()
        {
            if (!_selection.isEmpty)
            {
                DeleteSelection();
                return VisualSelectionRangeSnapShot.Empty;
            }

            if (_lineEditor.NewCharIndex < _lineEditor.Count)
            {
                _lineEditor.DoDelete();
            }
            else
            {
                //blank line
                //the bring to lower line to join with this line
                if (this.LineCount > 1 && _currentLineNo < this.LineCount - 1)
                {
                    //copy content from lower line to 
                    int pos = _lineEditor.NewCharIndex;
                    CurrentLineNumber++;//move to lower line
                    DoBackspace();
                    _lineEditor.NewCharIndex = pos;
                }
            }

            return VisualSelectionRangeSnapShot.Empty;
        }

        /// <summary>
        /// do end on current line
        /// </summary>
        public void DoEnd()
        {
            _lineEditor.NewCharIndex = _lineEditor.Count;
        }

        /// <summary>
        /// do home on current line
        /// </summary>
        public void DoHome()
        {
            //do home on current line
            _lineEditor.NewCharIndex = 0;
        }

        /// <summary>
        /// split current line into newline
        /// </summary>
        public void SplitIntoNewLine()
        {
            if (!_selection.isEmpty)
            {
                DeleteSelection();
            }

            if (_lineEditor.CharIndexOnTheEnd)
            {
                //end of current line
                _lines.Insert(CurrentLineNumber + 1, new PlainTextLine());
                CurrentLineNumber++;//move to lower
            }
            else
            {
                //split current line into 2
                _copyBuffer.Clear();
                _lineEditor.Split(_copyBuffer);

                //insert newline
                _lines.Insert(CurrentLineNumber + 1, new PlainTextLine());
                CurrentLineNumber++;//move to lower
                _lineEditor.AddText(_copyBuffer, 0, _copyBuffer.Length);
                _lineEditor.NewCharIndex = 0;//move to line start
            }
        }

        //------------------------------------------
        readonly PlainTextSelection _selection = new PlainTextSelection();
        /// <summary>
        /// start selection on current character index
        /// </summary>
        public void StartSelect()
        {
            _selection.StartSelect(_currentLineNo, _lineEditor.NewCharIndex);
        }
        /// <summary>
        /// end selection
        /// </summary>
        public void EndSelect()
        {
            _selection.EndSelect(_currentLineNo, _lineEditor.NewCharIndex);
        }
        public void CancelSelect()
        {
            _selection.CancelSelection();
        }
        public void TryMoveCaretTo(int charIndex, bool backward = false)
        {
            throw new NotImplementedException();
        }

        class PlainTextSelection
        {
            public int startLineNo;
            public int startLineNewCharIndex;//start line new char-index
            public int endLineNo;
            public int endLineNewCharIndex;//on end line
            public bool isEmpty = true;

            public void StartSelect(int startLineNo, int charIndex)
            {
                this.isEmpty = false;
                this.startLineNo = endLineNo = startLineNo;
                this.startLineNewCharIndex = endLineNewCharIndex = charIndex;
            }
            public void EndSelect(int endLineNo, int charIndex)
            {
                this.endLineNo = endLineNo;
                this.endLineNewCharIndex = charIndex;
            }
            public void CancelSelection()
            {
                this.isEmpty = true;
            }
            public void Finish()
            {
                this.isEmpty = true;
            }
            /// <summary>
            /// normalized selection
            /// </summary>
            public void Normalize()
            {
                if (endLineNo < startLineNo)
                {
                    //swap
                    int temp = startLineNo;
                    startLineNo = endLineNo;
                    endLineNo = temp;
                    //
                    //force swap start and end char index
                    temp = endLineNewCharIndex;
                    startLineNewCharIndex = endLineNewCharIndex;
                    endLineNewCharIndex = temp;

                }
                else if (endLineNo == startLineNo)
                {
                    //on the sameline
                    if (endLineNewCharIndex < startLineNewCharIndex)
                    {
                        int temp = endLineNewCharIndex;
                        startLineNewCharIndex = endLineNewCharIndex;
                        endLineNewCharIndex = temp;
                    }
                }
            }

        }

        //----
        public void CopyAll(TextCopyBufferUtf32 output)
        {
            //all
            int j = _lines.Count;
            for (int i = 0; i < j; ++i)
            {
                if (i == _currentLineNo)
                {
                    _lineEditor.Read(output);
                }
                else
                {
                    _lines[i].Content.WriteTo(output);
                }
            }
        }
        public void CopySelection(TextCopyBufferUtf32 output)
        {
            //only selection
            if (_selection.isEmpty) { return; }

            int currentLine = _currentLineNo;
            _selection.Normalize();
            if (_selection.startLineNo == _selection.endLineNo)
            {
                //on the sameline
                CurrentLineNumber = _selection.startLineNo;
                _lineEditor.Read(output, _selection.startLineNewCharIndex, _selection.endLineNewCharIndex - _selection.startLineNewCharIndex);
            }
            else
            {
                //more than 1 line
                CurrentLineNumber = _selection.startLineNo;
                _lineEditor.Read(output, _selection.startLineNewCharIndex, _lineEditor.Count - _selection.startLineNewCharIndex);
                //
                int endLineNo = _selection.endLineNo;
                if ((endLineNo - _selection.startLineNo - 1) > 0)
                {

                    for (int i = _selection.startLineNo + 1; i < endLineNo; ++i)
                    {
                        _lines[i].Content.WriteTo(output);
                    }
                }

                CurrentLineNumber = endLineNo;
                _lineEditor.Read(output, 0, _selection.endLineNewCharIndex);
                CurrentLineNumber = currentLine;//gotback
            }

        }
    }
}
