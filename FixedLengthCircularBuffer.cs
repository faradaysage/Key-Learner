using KeyLearner.Elements;
using System.Collections.Generic;

public class FixedLengthCircularBuffer
{
    private readonly (char Character, TextElement Element)[] _buffer;
    private int _start;
    private int _size;

    public FixedLengthCircularBuffer(int capacity)
    {
        _buffer = new (char Character, TextElement Element)[capacity];
        _start = 0;
        _size = 0;
    }

    public void Add(char c, TextElement element)
    {
        int index = (_start + _size) % _buffer.Length;
        _buffer[index] = (c.ToString().ToUpper().ToCharArray()[0], element);

        if (_size < _buffer.Length)
        {
            _size++;
        }
        else
        {
            _start = (_start + 1) % _buffer.Length;
        }
    }

    public List<(char Character, TextElement Element)> GetContents()
    {
        var result = new List<(char Character, TextElement Element)>();
        for (int i = 0; i < _size; i++)
        {
            result.Add(_buffer[(_start + i) % _buffer.Length]);
        }
        return result;
    }

    public string GetStringContents()
    {
        var chars = new char[_size];
        for (int i = 0; i < _size; i++)
        {
            chars[i] = _buffer[(_start + i) % _buffer.Length].Character;
        }
        return new string(chars);
    }

    public List<TextElement> GetTextElements(string word)
    {
        if (string.IsNullOrEmpty(word) || word.Length > _size)
        {
            return new List<TextElement>(); // Return empty list if invalid
        }

        var wordLength = word.Length;
        var matchedElements = new List<TextElement>();

        // Traverse the buffer from the end
        for (int i = _size - 1, wordIndex = wordLength - 1; wordIndex >= 0; i--, wordIndex--)
        {
            var bufferIndex = (_start + i) % _buffer.Length;

            if (_buffer[bufferIndex].Character != word[wordIndex])
            {
                return new List<TextElement>(); // Mismatch, return empty list
            }

            // Collect matching TextElement
            matchedElements.Add(_buffer[bufferIndex].Element);
        }

        // Reverse to maintain left-to-right order of the word
        matchedElements.Reverse();
        return matchedElements;
    }
}
