
USE [master]
GO
/****** Object:  Database [AccsoCourier]    Script Date: 8/23/2026 9:23:04 PM ******/
CREATE DATABASE [AccsoCourier]
 CONTAINMENT = NONE
 ON  PRIMARY 
( NAME = N'AccsoCourier', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL16.CHARLESNOLTE\MSSQL\DATA\AccsoCourier.mdf' , SIZE = 8192KB , MAXSIZE = UNLIMITED, FILEGROWTH = 65536KB )
 LOG ON 
( NAME = N'AccsoCourier_log', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL16.CHARLESNOLTE\MSSQL\DATA\AccsoCourier_log.ldf' , SIZE = 8192KB , MAXSIZE = 2048GB , FILEGROWTH = 65536KB )
 WITH CATALOG_COLLATION = DATABASE_DEFAULT, LEDGER = OFF
GO
ALTER DATABASE [AccsoCourier] SET COMPATIBILITY_LEVEL = 160
GO
IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [AccsoCourier].[dbo].[sp_fulltext_database] @action = 'enable'
end
GO
ALTER DATABASE [AccsoCourier] SET ANSI_NULL_DEFAULT OFF 
GO
ALTER DATABASE [AccsoCourier] SET ANSI_NULLS OFF 
GO
ALTER DATABASE [AccsoCourier] SET ANSI_PADDING OFF 
GO
ALTER DATABASE [AccsoCourier] SET ANSI_WARNINGS OFF 
GO
ALTER DATABASE [AccsoCourier] SET ARITHABORT OFF 
GO
ALTER DATABASE [AccsoCourier] SET AUTO_CLOSE OFF 
GO
ALTER DATABASE [AccsoCourier] SET AUTO_SHRINK OFF 
GO
ALTER DATABASE [AccsoCourier] SET AUTO_UPDATE_STATISTICS ON 
GO
ALTER DATABASE [AccsoCourier] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO
ALTER DATABASE [AccsoCourier] SET CURSOR_DEFAULT  GLOBAL 
GO
ALTER DATABASE [AccsoCourier] SET CONCAT_NULL_YIELDS_NULL OFF 
GO
ALTER DATABASE [AccsoCourier] SET NUMERIC_ROUNDABORT OFF 
GO
ALTER DATABASE [AccsoCourier] SET QUOTED_IDENTIFIER OFF 
GO
ALTER DATABASE [AccsoCourier] SET RECURSIVE_TRIGGERS OFF 
GO
ALTER DATABASE [AccsoCourier] SET  DISABLE_BROKER 
GO
ALTER DATABASE [AccsoCourier] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO
ALTER DATABASE [AccsoCourier] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO
ALTER DATABASE [AccsoCourier] SET TRUSTWORTHY OFF 
GO
ALTER DATABASE [AccsoCourier] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO
ALTER DATABASE [AccsoCourier] SET PARAMETERIZATION SIMPLE 
GO
ALTER DATABASE [AccsoCourier] SET READ_COMMITTED_SNAPSHOT OFF 
GO
ALTER DATABASE [AccsoCourier] SET HONOR_BROKER_PRIORITY OFF 
GO
ALTER DATABASE [AccsoCourier] SET RECOVERY FULL 
GO
ALTER DATABASE [AccsoCourier] SET  MULTI_USER 
GO
ALTER DATABASE [AccsoCourier] SET PAGE_VERIFY CHECKSUM  
GO
ALTER DATABASE [AccsoCourier] SET DB_CHAINING OFF 
GO
ALTER DATABASE [AccsoCourier] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF ) 
GO
ALTER DATABASE [AccsoCourier] SET TARGET_RECOVERY_TIME = 60 SECONDS 
GO
ALTER DATABASE [AccsoCourier] SET DELAYED_DURABILITY = DISABLED 
GO
ALTER DATABASE [AccsoCourier] SET ACCELERATED_DATABASE_RECOVERY = OFF  
GO
EXEC sys.sp_db_vardecimal_storage_format N'AccsoCourier', N'ON'
GO
ALTER DATABASE [AccsoCourier] SET QUERY_STORE = ON
GO
ALTER DATABASE [AccsoCourier] SET QUERY_STORE (OPERATION_MODE = READ_WRITE, CLEANUP_POLICY = (STALE_QUERY_THRESHOLD_DAYS = 30), DATA_FLUSH_INTERVAL_SECONDS = 900, INTERVAL_LENGTH_MINUTES = 60, MAX_STORAGE_SIZE_MB = 1000, QUERY_CAPTURE_MODE = AUTO, SIZE_BASED_CLEANUP_MODE = AUTO, MAX_PLANS_PER_QUERY = 200, WAIT_STATS_CAPTURE_MODE = ON)
GO
USE [AccsoCourier]
GO
/****** Object:  Table [dbo].[Shipment]    Script Date: 8/23/2026 9:23:05 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Shipment](
	[ShipmentId] [nvarchar](100) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_Shipment] PRIMARY KEY CLUSTERED 
(
	[ShipmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ShipmentEvent]    Script Date: 8/23/2026 9:23:05 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ShipmentEvent](
	[ShipmentEventId] [bigint] IDENTITY(1,1) NOT NULL,
	[EventId] [nvarchar](200) NOT NULL,
	[Partner] [nvarchar](100) NOT NULL,
	[ShipmentId] [nvarchar](100) NOT NULL,
	[Status] [nvarchar](50) NOT NULL,
	[OccurredAt] [datetime2](7) NOT NULL,
	[ReceivedAt] [datetime2](7) NOT NULL,
	[Location] [nvarchar](200) NULL,
	[ProcessingStatus] [nvarchar](30) NOT NULL,
	[ConflictReason] [nvarchar](1000) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_ShipmentEvent] PRIMARY KEY CLUSTERED 
(
	[ShipmentEventId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ShipmentState]    Script Date: 8/23/2026 9:23:05 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ShipmentState](
	[ShipmentId] [nvarchar](100) NOT NULL,
	[CurrentStatus] [nvarchar](50) NOT NULL,
	[CurrentEventId] [nvarchar](200) NOT NULL,
	[CurrentOccurredAt] [datetime2](7) NOT NULL,
	[Location] [nvarchar](200) NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
	[RowVersion] [timestamp] NOT NULL,
 CONSTRAINT [PK_ShipmentState] PRIMARY KEY CLUSTERED 
(
	[ShipmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
INSERT [dbo].[Shipment] ([ShipmentId], [CreatedAt]) VALUES (N'ship-456', CAST(N'2026-08-23T20:06:28.8521607' AS DateTime2))
GO
INSERT [dbo].[Shipment] ([ShipmentId], [CreatedAt]) VALUES (N'ship-789', CAST(N'2026-08-23T20:25:09.4408395' AS DateTime2))
GO
SET IDENTITY_INSERT [dbo].[ShipmentEvent] ON 
GO
INSERT [dbo].[ShipmentEvent] ([ShipmentEventId], [EventId], [Partner], [ShipmentId], [Status], [OccurredAt], [ReceivedAt], [Location], [ProcessingStatus], [ConflictReason], [CreatedAt]) VALUES (1, N'evt-123', N'dhl', N'ship-456', N'IN_TRANSIT', CAST(N'2026-03-10T12:00:00.0000000' AS DateTime2), CAST(N'2026-03-10T12:00:05.0000000' AS DateTime2), N'Amsterdam', N'APPLIED', NULL, CAST(N'2026-08-23T20:07:14.3786245' AS DateTime2))
GO
INSERT [dbo].[ShipmentEvent] ([ShipmentEventId], [EventId], [Partner], [ShipmentId], [Status], [OccurredAt], [ReceivedAt], [Location], [ProcessingStatus], [ConflictReason], [CreatedAt]) VALUES (3, N'evt-789', N'dhl', N'ship-789', N'HANDED_TO_CARRIER', CAST(N'2026-08-24T20:23:06.8410000' AS DateTime2), CAST(N'2026-08-24T20:23:06.8410000' AS DateTime2), N'Cape Town', N'APPLIED', NULL, CAST(N'2026-08-23T20:25:09.4706407' AS DateTime2))
GO
SET IDENTITY_INSERT [dbo].[ShipmentEvent] OFF
GO
INSERT [dbo].[ShipmentState] ([ShipmentId], [CurrentStatus], [CurrentEventId], [CurrentOccurredAt], [Location], [UpdatedAt]) VALUES (N'ship-456', N'IN_TRANSIT', N'evt-123', CAST(N'2026-03-10T12:00:00.0000000' AS DateTime2), N'Amsterdam', CAST(N'2026-08-23T20:07:26.9673498' AS DateTime2))
GO
INSERT [dbo].[ShipmentState] ([ShipmentId], [CurrentStatus], [CurrentEventId], [CurrentOccurredAt], [Location], [UpdatedAt]) VALUES (N'ship-789', N'HANDED_TO_CARRIER', N'evt-789', CAST(N'2026-08-24T20:23:06.8410000' AS DateTime2), N'Cape Town', CAST(N'2026-08-23T20:25:09.4869053' AS DateTime2))
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_ShipmentEvent_ProcessingStatus]    Script Date: 8/23/2026 9:23:05 PM ******/
CREATE NONCLUSTERED INDEX [IX_ShipmentEvent_ProcessingStatus] ON [dbo].[ShipmentEvent]
(
	[ProcessingStatus] ASC
)
WHERE ([ProcessingStatus]='CONFLICT')
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_ShipmentEvent_ShipmentId_OccurredAt]    Script Date: 8/23/2026 9:23:05 PM ******/
CREATE NONCLUSTERED INDEX [IX_ShipmentEvent_ShipmentId_OccurredAt] ON [dbo].[ShipmentEvent]
(
	[ShipmentId] ASC,
	[OccurredAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_ShipmentEvent_Partner_EventId]    Script Date: 8/23/2026 9:23:05 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_ShipmentEvent_Partner_EventId] ON [dbo].[ShipmentEvent]
(
	[Partner] ASC,
	[EventId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Shipment] ADD  CONSTRAINT [DF_Shipment_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ShipmentEvent] ADD  CONSTRAINT [DF_ShipmentEvent_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ShipmentState] ADD  CONSTRAINT [DF_ShipmentState_UpdatedAt]  DEFAULT (sysutcdatetime()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[ShipmentEvent]  WITH CHECK ADD  CONSTRAINT [FK_ShipmentEvent_Shipment] FOREIGN KEY([ShipmentId])
REFERENCES [dbo].[Shipment] ([ShipmentId])
GO
ALTER TABLE [dbo].[ShipmentEvent] CHECK CONSTRAINT [FK_ShipmentEvent_Shipment]
GO
ALTER TABLE [dbo].[ShipmentState]  WITH CHECK ADD  CONSTRAINT [FK_ShipmentState_Shipment] FOREIGN KEY([ShipmentId])
REFERENCES [dbo].[Shipment] ([ShipmentId])
GO
ALTER TABLE [dbo].[ShipmentState] CHECK CONSTRAINT [FK_ShipmentState_Shipment]
GO
ALTER TABLE [dbo].[ShipmentEvent]  WITH CHECK ADD  CONSTRAINT [CK_ShipmentEvent_ProcessingStatus] CHECK  (([ProcessingStatus]='CONFLICT' OR [ProcessingStatus]='STALE' OR [ProcessingStatus]='DUPLICATE' OR [ProcessingStatus]='APPLIED'))
GO
ALTER TABLE [dbo].[ShipmentEvent] CHECK CONSTRAINT [CK_ShipmentEvent_ProcessingStatus]
GO
ALTER TABLE [dbo].[ShipmentEvent]  WITH CHECK ADD  CONSTRAINT [CK_ShipmentEvent_Status] CHECK  (([Status]='RETURNED' OR [Status]='DELIVERY_EXCEPTION' OR [Status]='DELIVERED' OR [Status]='OUT_FOR_DELIVERY' OR [Status]='IN_TRANSIT' OR [Status]='HANDED_TO_CARRIER' OR [Status]='LABEL_CREATED'))
GO
ALTER TABLE [dbo].[ShipmentEvent] CHECK CONSTRAINT [CK_ShipmentEvent_Status]
GO
ALTER TABLE [dbo].[ShipmentState]  WITH CHECK ADD  CONSTRAINT [CK_ShipmentState_Status] CHECK  (([CurrentStatus]='RETURNED' OR [CurrentStatus]='DELIVERY_EXCEPTION' OR [CurrentStatus]='DELIVERED' OR [CurrentStatus]='OUT_FOR_DELIVERY' OR [CurrentStatus]='IN_TRANSIT' OR [CurrentStatus]='HANDED_TO_CARRIER' OR [CurrentStatus]='LABEL_CREATED'))
GO
ALTER TABLE [dbo].[ShipmentState] CHECK CONSTRAINT [CK_ShipmentState_Status]
GO
USE [master]
GO
ALTER DATABASE [AccsoCourier] SET  READ_WRITE 
GO
