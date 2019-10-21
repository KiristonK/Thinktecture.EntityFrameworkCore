﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Thinktecture.TestDatabaseContext;

namespace Thinktecture.Migrations
{
    [DbContext(typeof(TestDbContext))]
    [Migration("20191010194144_Add_ConvertibleClass")]
    partial class Add_ConvertibleClass
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.0.0");

            modelBuilder.Entity("Thinktecture.TestDatabaseContext.TestEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<int?>("ConvertibleClass")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Count")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<int>("PropertyWithBackingField")
                        .HasColumnType("INTEGER");

                    b.Property<int>("_privateField")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("TestEntities");
                });

            modelBuilder.Entity("Thinktecture.TestDatabaseContext.TestEntityWithAutoIncrement", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("TestEntitiesWithAutoIncrement");
                });

            modelBuilder.Entity("Thinktecture.TestDatabaseContext.TestEntityWithShadowProperties", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<int?>("ShadowIntProperty")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ShadowStringProperty")
                        .HasColumnType("TEXT")
                        .HasMaxLength(50);

                    b.HasKey("Id");

                    b.ToTable("TestEntitiesWithShadowProperties");
                });
#pragma warning restore 612, 618
        }
    }
}