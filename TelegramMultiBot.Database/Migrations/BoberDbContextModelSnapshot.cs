﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TelegramMultiBot.Database;

#nullable disable

namespace TelegramMultiBot.Database.Migrations
{
    [DbContext(typeof(BoberDbContext))]
    partial class BoberDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("ImageJob", b =>
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

                    b.Property<DateTime>("Finised")
                        .HasColumnType("datetime(6)");

                    b.Property<int>("MessageId")
                        .HasColumnType("int");

                    b.Property<int?>("MessageThreadId")
                        .HasColumnType("int");

                    b.Property<bool>("PostInfo")
                        .HasColumnType("tinyint(1)");

                    b.Property<Guid?>("PreviousJobResultId")
                        .HasColumnType("char(36)");

                    b.Property<DateTime>("Started")
                        .HasColumnType("datetime(6)");

                    b.Property<int>("Status")
                        .HasColumnType("int");

                    b.Property<string>("Text")
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

            modelBuilder.Entity("JobResult", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<string>("FilePath")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("Index")
                        .HasColumnType("int");

                    b.Property<string>("Info")
                        .HasColumnType("longtext");

                    b.Property<Guid>("JobId")
                        .HasColumnType("char(36)");

                    b.Property<TimeSpan>("RenderTime")
                        .HasColumnType("time(6)");

                    b.HasKey("Id");

                    b.HasIndex("JobId");

                    b.ToTable("JobResult");
                });

            modelBuilder.Entity("JobResult", b =>
                {
                    b.HasOne("ImageJob", "Job")
                        .WithMany("Results")
                        .HasForeignKey("JobId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Job");
                });

            modelBuilder.Entity("ImageJob", b =>
                {
                    b.Navigation("Results");
                });
#pragma warning restore 612, 618
        }
    }
}
