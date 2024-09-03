using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramMultiBot.ImageCompare
{
    internal static class ImageComparator
    {      

        public static bool Compare(byte[] imageOne, byte[] imageTwo)
        {
            if (imageOne == null || imageTwo == null)
                return false;

            if(imageOne.Length != imageTwo.Length)
                return false;

            for (int i = 0; i < imageOne.Length; i++)
            {
                if(imageOne[i] != imageTwo[i])
                    return false;
            }

            return true;
        }
    }
}
