using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using NxtTipbot;

namespace NxtTipbot.Migrations
{
    [DbContext(typeof(WalletContext))]
    partial class WalletContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.0.0-rtm-21431");

            modelBuilder.Entity("NxtTipbot.Model.NxtAccount", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("id");

                    b.Property<string>("NxtAccountRs")
                        .IsRequired()
                        .HasColumnName("nxt_address");

                    b.Property<string>("SecretPhrase")
                        .IsRequired()
                        .HasColumnName("secret_phrase");

                    b.Property<string>("SlackId")
                        .IsRequired()
                        .HasColumnName("slack_id");

                    b.HasKey("Id");

                    b.ToTable("account");
                });

            modelBuilder.Entity("NxtTipbot.Model.Setting", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("id");

                    b.Property<string>("Key")
                        .IsRequired()
                        .HasColumnName("key");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnName("value");

                    b.HasKey("Id");

                    b.ToTable("setting");
                });
        }
    }
}
