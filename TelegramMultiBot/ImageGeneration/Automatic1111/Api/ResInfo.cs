namespace TelegramMultiBot.ImageGenerators.Automatic1111.Api
{
    public class ResInfo
    {
        public string? prompt { get; set; }
        public string[]? all_prompts { get; set; }
        public string? negative_prompt { get; set; }
        public string[]? all_negative_prompts { get; set; }
        public long seed { get; set; }
        public long[]? all_seeds { get; set; }
        public long subseed { get; set; }
        public long[]? all_subseeds { get; set; }
        public int? subseed_strength { get; set; }
        public int? width { get; set; }
        public int? height { get; set; }
        public string? sampler_name { get; set; }
        public double? cfg_scale { get; set; }
        public int? steps { get; set; }
        public int? batch_size { get; set; }
        public bool restore_faces { get; set; }
        public object? face_restoration_model { get; set; }
        public string? sd_model_name { get; set; }
        public string? sd_model_hash { get; set; }
        public object? sd_vae_name { get; set; }
        public object? sd_vae_hash { get; set; }
        public int? seed_resize_from_w { get; set; }
        public int? seed_resize_from_h { get; set; }
        public double? denoising_strength { get; set; }
        public Extra_Generation_Params? extra_generation_params { get; set; }
        public int? index_of_first_image { get; set; }
        public string[]? infotexts { get; set; }
        public object[]? styles { get; set; }
        public string? job_timestamp { get; set; }
        public int? clip_skip { get; set; }
        public bool is_using_inpainting_conditioning { get; set; }
        public string? image { get; set; }
        public string? html_info { get; set; }
    }
}
