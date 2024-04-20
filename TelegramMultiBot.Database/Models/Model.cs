﻿using System.ComponentModel.DataAnnotations;
using TelegramMultiBot.Database.Enums;

namespace TelegramMultiBot.Database.Models
{

    public class Model
    {
        [Key]
        public string Name { get; set; }
        public string Path { get; set; }
        public float CGF { get; set; }
        public int Steps { get; set; }
        public string Sampler { get; set; }
        public string Scheduler { get; set; }
        public int CLIPskip { get; set; } = 1;
        public ModelVersion Version { get; set; }
    }
}