using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramMultiBot.AiAssistant
{
    public interface IPhrasesService
    {
        string GetRandomServiceUnavailablePhrase();
        string GetRandomTimeoutPhrase();
    }

    public class PhrasesService : IPhrasesService
    {
        private List<string> _serviceUnavailablePhrases = new List<string>();
        private List<string> _timeoutPhrases= new List<string>();

        public PhrasesService()
        {
            try
            {
               var lines =  File.ReadAllLines("phrases.txt").Select(x=>x.Trim()).ToArray();
                List<string> currentArray = null;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i] == "[Service Unavailable]")
                    {
                        currentArray = _serviceUnavailablePhrases;
                    }
                    else if (lines[i] == "[Timeout]")
                    {
                        currentArray = _timeoutPhrases;
                    }
                    else if (!string.IsNullOrWhiteSpace(lines[i].Trim()) && currentArray != null)
                    {
                        currentArray.Add(lines[i].Trim());
                    }
                }
            }
            catch (Exception)
            {
                _serviceUnavailablePhrases = ["В мене лапки :("];
                _timeoutPhrases = ["Час вийшов :("];
            }
        }

        public string GetRandomServiceUnavailablePhrase()
        {
            return GetRandomPhrase(_serviceUnavailablePhrases);
        }

        public string GetRandomTimeoutPhrase()
        {
            return GetRandomPhrase(_timeoutPhrases);
        }

        private string GetRandomPhrase(List<string> target)
        {
            if (target == null || target.Count == 0)
            {
                return "No phrases available";
            }
            return target[Random.Shared.Next(target.Count)];
        }
    }
}
