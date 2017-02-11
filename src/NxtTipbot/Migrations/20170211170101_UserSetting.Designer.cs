using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using NxtTipbot;

namespace NxtTipbot.Migrations
{
    [DbContext(typeof(WalletContext))]
    [Migration("20170211170101_UserSetting")]
    partial class UserSetting
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

            modelBuilder.Entity("NxtTipbot.Model.UserSetting", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("id");

                    b.Property<int>("AccountId")
                        .HasColumnName("account_id");

                    b.Property<string>("Key")
                        .HasColumnName("key");

                    b.Property<string>("Value")
                        .HasColumnName("value");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.ToTable("user_setting");
                });

            modelBuilder.Entity("NxtTipbot.Model.UserSetting", b =>
                {
                    b.HasOne("NxtTipbot.Model.NxtAccount", "Account")
                        .WithMany("UserSettings")
                        .HasForeignKey("AccountId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
        }
    }
}
