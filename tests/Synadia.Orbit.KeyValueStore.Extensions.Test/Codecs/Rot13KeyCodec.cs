// Copyright (c) Synadia Communications, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Synadia.Orbit.KeyValueStore.Extensions.Codecs;

namespace Synadia.Orbit.KeyValueStore.Extensions.Test.Codecs;

/// <summary>
/// A custom codec that "encrypts" keys using ROT13 substitution cipher.
/// This is for demonstration purposes only - ROT13 is not secure encryption.
/// </summary>
internal sealed class Rot13KeyCodec : INatsFilterableKeyCodec
{
    public string EncodeKey(string key) => Rot13(key);

    public string DecodeKey(string key) => Rot13(key);

    public string EncodeFilter(string filter) => Rot13(filter);

    private static string Rot13(string input)
    {
        char[] result = new char[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c is >= 'a' and <= 'z')
            {
                result[i] = (char)('a' + ((c - 'a' + 13) % 26));
            }
            else if (c is >= 'A' and <= 'Z')
            {
                result[i] = (char)('A' + ((c - 'A' + 13) % 26));
            }
            else
            {
                result[i] = c;
            }
        }

        return new string(result);
    }
}
