//Copyright (c) 2026 Denys Dragunov, dragunovdenis@gmail.com
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files(the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and /or sell
//copies of the Software, and to permit persons to whom the Software is furnished
//to do so, subject to the following conditions :

//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
//INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
//PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
//HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
//OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

namespace BAnalyzerCore.Ollama;

/// <summary>
/// The "prompt engineering" of the speech-to-text requests: what gets sent to
/// an audio-capable model and how its answer gets cleaned up.
/// </summary>
internal static class SpeechTranscription
{
    /// <summary>
    /// System prompt of a transcription request. Without it the audio models
    /// tend to <i>answer</i> the spoken question instead of transcribing it.
    /// Keep it short: longer, stricter prompts made the small audio models cut the transcription off mid-sentence.
    /// </summary>
    internal const string SystemPrompt =
        "You are a speech-to-text transcriber. Output only the words spoken in the audio.";

    /// <summary>
    /// User instruction that accompanies the audio of a transcription request.
    /// </summary>
    internal const string Instruction = "Transcribe this audio verbatim. Do not answer it.";

    /// <summary>
    /// Characters trimmed from both ends of a transcription.
    /// </summary>
    private static readonly char[] TrimChars = " \t\r\n\"'“”«»".ToCharArray();

    /// <summary>
    /// Noise some audio models put in front of a transcription
    /// (the small ones are also prone to echo the instruction back).
    /// </summary>
    private static readonly string[] LeadingNoise = ["Transcription:", "Transcript:", Instruction];

    /// <summary>
    /// Returns the messages of a request to transcribe the given WAV audio.
    /// </summary>
    internal static List<OllamaChatMessageDto> BuildMessages(byte[] wavAudio) =>
    [
        new() { Role = ChatRoles.System, Content = SystemPrompt },
        new() { Role = ChatRoles.User, Content = Instruction, Images = [Convert.ToBase64String(wavAudio)] }
    ];

    /// <summary>
    /// Cleans the "raw" output of an audio model up: removes the surrounding
    /// quotes and white spaces, the "Transcription:" prefixes and the
    /// <see cref="Instruction"/> if the model echoed it back.
    /// </summary>
    internal static string Clean(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var text = raw.Trim(TrimChars);
        bool changed;

        do
        {
            changed = false;

            foreach (var noise in LeadingNoise)
            {
                if (text.StartsWith(noise, StringComparison.OrdinalIgnoreCase))
                {
                    text = text[noise.Length..].Trim(TrimChars);
                    changed = true;
                }
            }

            if (text.EndsWith(Instruction, StringComparison.OrdinalIgnoreCase))
            {
                text = text[..^Instruction.Length].Trim(TrimChars);
                changed = true;
            }
        } while (changed && text.Length > 0);

        return text;
    }
}
