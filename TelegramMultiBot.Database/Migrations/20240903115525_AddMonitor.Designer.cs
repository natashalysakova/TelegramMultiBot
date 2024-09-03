﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TelegramMultiBot.Database;

#nullable disable

namespace TelegramMultiBot.Database.Migrations
{
    [DbContext(typeof(BoberDbContext))]
    [Migration("20240903115525_AddMonitor")]
    partial class AddMonitor
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            MySqlModelBuilderExtensions.AutoIncrementColumns(modelBuilder);

            modelBuilder.Entity("TelegramMultiBot.Database.Models.BotMessage", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<long>("ChatId")
                        .HasColumnType("bigint");

                    b.Property<bool>("IsPrivateChat")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("MessageId")
                        .HasColumnType("int");

                    b.Property<DateTime>("SendTime")
                        .HasColumnType("datetime(6)");

                    b.Property<long?>("UserId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.ToTable("BotMessages");
                });

            modelBuilder.Entity("TelegramMultiBot.Database.Models.Host", b =>
                {
                    b.Property<string>("Address")
                        .HasColumnType("varchar(255)");

                    b.Property<int>("Port")
                        .HasColumnType("int");

                    b.Property<bool>("Enabled")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("Priority")
                        .HasColumnType("int");

                    b.Property<string>("Protocol")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("UI")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Address", "Port");

                    b.ToTable("Hosts");
                });

            modelBuilder.Entity("TelegramMultiBot.Database.Models.ImageJob", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<int>("BotMessageId")
                        .HasColumnType("int");

                    b.Property<long>("ChatId")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("Created")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("Diffusor")
                        .HasColumnType("longtext");

                    b.Property<DateTime>("Finised")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("InputImage")
                        .HasColumnType("longtext");

                    b.Property<int>("MessageId")
                        .HasColumnType("int");

                    b.Property<int?>("MessageThreadId")
                        .HasColumnType("int");

                    b.Property<DateTime>("NextTry")
                        .HasColumnType("datetime(6)");

                    b.Property<bool>("PostInfo")
                        .HasColumnType("tinyint(1)");

                    b.Property<Guid?>("PreviousJobResultId")
                        .HasColumnType("char(36)");

                    b.Property<double>("Progress")
                        .HasColumnType("double");

                    b.Property<DateTime>("Started")
                        .HasColumnType("datetime(6)");

                    b.Property<int>("Status")
                        .HasColumnType("int");

                    b.Property<string>("Text")
                        .HasColumnType("longtext");

                    b.Property<string>("TextStatus")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("Type")
                        .HasColumnType("int");

                    b.Property<double?>("UpscaleModifyer")
                        .HasColumnType("double");

                    b.Property<long>("UserId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.ToTable("Jobs");
                });

            modelBuilder.Entity("TelegramMultiBot.Database.Models.JobResult", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<string>("FileId")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("FilePath")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("Index")
                        .HasColumnType("int");

                    b.Property<string>("Info")
                        .HasColumnType("longtext");

                    b.Property<Guid>("JobId")
                        .HasColumnType("char(36)");

                    b.Property<double>("RenderTime")
                        .HasColumnType("double");

                    b.HasKey("Id");

                    b.HasIndex("JobId");

                    b.ToTable("JobResult");
                });

            modelBuilder.Entity("TelegramMultiBot.Database.Models.Model", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("varchar(255)");

                    b.Property<float>("CGF")
                        .HasColumnType("float");

                    b.Property<int>("CLIPskip")
                        .HasColumnType("int");

                    b.Property<string>("Path")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Sampler")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Scheduler")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("Steps")
                        .HasColumnType("int");

                    b.Property<int>("Version")
                        .HasColumnType("int");

                    b.HasKey("Name");

                    b.ToTable("Models");
                });

            modelBuilder.Entity("TelegramMultiBot.Database.Models.MonitorJob", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("Id"));

                    b.Property<long>("ChatId")
                        .HasColumnType("bigint");

                    b.Property<string>("DeactivationReason")
                        .HasColumnType("longtext");

                    b.Property<bool>("IsActive")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("Monitor");
                });

            modelBuilder.Entity("TelegramMultiBot.Database.Models.ReminderJob", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<long>("ChatId")
                        .HasColumnType("bigint");

                    b.Property<string>("Config")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("FileId")
                        .HasColumnType("longtext");

                    b.Property<string>("Message")
                        .HasColumnType("longtext");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<DateTime>("NextExecution")
                        .HasColumnType("datetime(6)");

                    b.HasKey("Id");

                    b.ToTable("Reminders");
                });

            modelBuilder.Entity("TelegramMultiBot.Database.Models.Settings", b =>
                {
                    b.Property<string>("SettingSection")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("SettingsKey")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("SettingsValue")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("SettingSection", "SettingsKey");

                    b.ToTable("Settings");
                });

            modelBuilder.Entity("TelegramMultiBot.Database.Models.JobResult", b =>
                {
                    b.HasOne("TelegramMultiBot.Database.Models.ImageJob", "Job")
                        .WithMany("Results")
                        .HasForeignKey("JobId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Job");
                });

            modelBuilder.Entity("TelegramMultiBot.Database.Models.ImageJob", b =>
                {
                    b.Navigation("Results");
                });
#pragma warning restore 612, 618
        }
    }
}
