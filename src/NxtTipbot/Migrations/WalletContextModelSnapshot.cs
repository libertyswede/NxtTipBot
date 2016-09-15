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

            modelBuilder.Entity("NxtTipbot.NxtAccount", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd();

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
        }
    }
}
