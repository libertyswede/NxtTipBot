using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using NxtTipbot;

namespace NxtTipbot.Migrations
{
    [DbContext(typeof(WalletContext))]
    [Migration("20161002043855_Initial")]
    partial class Initial
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.0.0-rtm-21431");

            modelBuilder.Entity("NxtTipbot.Model.NxtAccount", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("id");

                    b.Property<string>("NxtAccountRs")
                        .HasColumnName("nxt_address");

                    b.Property<string>("SlackId")
                        .HasColumnName("slack_id");

                    b.HasKey("Id");

                    b.ToTable("account");
                });
        }
    }
}
