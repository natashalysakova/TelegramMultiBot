using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramMultiBot.AiAssistant
{
    public interface IPhrasesService
    {
        string GetRandomPhrase();
    }

    public class PhrasesService : IPhrasesService
    {
        private string[] _phrases;

        public PhrasesService()
        {
            try
            {
                _phrases = File.ReadAllLines("phrases.txt").Select(x=>x.Trim()).ToArray();
            }
            catch (Exception)
            {
                _phrases = ["В мене лапки :("];
            }
        }

        public string GetRandomPhrase()
        {
            return _phrases[Random.Shared.Next(_phrases.Length)];
        }
    }
}
