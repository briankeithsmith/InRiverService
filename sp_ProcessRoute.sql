USE [BHCCRM]
GO

/****** Object:  StoredProcedure [dbo].[sp_ProcessRoute]    Script Date: 1/11/2017 11:44:41 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO





ALTER   procedure [dbo].[sp_ProcessRoute]
@CallerDN varchar(15),
@CalledDN varchar(15),
@CollectedDigits  varchar(15),
@UUI varchar(96) 
--Per email from Stevie on Friday 12/28/2007, return a dataset with 1 row, 2 cols, UUI, DEST
-- OUTPUT
--@DEST varchar(4) OUTPUT
as
declare @UUI_OUT as varchar(96)
declare @DEST_VDN as varchar(4)

declare @UUIOriginal varchar(96)
declare @AvayaDNIS  char(4)
declare @ANI varchar(10)
declare @DNIS varchar(15)
declare @PremCust char(1)
declare @CustAcct varchar(9)
declare @CustName varchar(30)
declare @Contact varchar(29)
declare @SalesRep varchar(6)
declare @UUIelem int
declare @currVal varchar(50)
declare @ActionCode int
declare @VDNout int
declare @AcctTemp varchar(15)
declare @SalesRepTemp varchar(6)
declare @CSRTemp varchar(6) --BKS added on 10/9/2008 for CSR Support
declare @count int
declare @DEST varchar(4)
declare @sql varchar(500)
declare @ActionRouteCompanyID int --The company assigned to the incoming VDN
declare @CompanyID int
declare @CompanyCount int
declare @ProcessPath int -- use this flag to indicate how many we have to process
declare @Override varchar(6)

begin

--BKS made change on 5/1/2013 for areacoderepcode lookup where I added CompanyID=@ActionRouteCompanyID to the clause

--This stored procedure is written for the Avaya Phone System
--General Flow
--	1. UUI/DEST fields come in
--	2. Perform lookup against ActionRouteTable for VDN
--	3. Based on ActionCode, perform one of the following
--		a. 0 - straight through based on information in ActionRoute
--		b. 1 - Get Customer - return customer data from datahub based on phone#
--		c. 2 - Get Customer AND Agent Same as #1 plus return agent info for that customer
--		d. 3 - Get Customer AND route based on AREA Code to Sales Rep (BHCCRM..AreaCodeRepCode)
--		e. 4 - Get Customer AND route based on CSR assigned to account.  Same as #1,#2, but we look at CSRRepCode in Unidata_no table instead of SalesRep (Sales Rep code becomes a backup at that point).
--Initialize variables
		select @DEST='0000', @UUIelem=0, @AvayaDNIS='', @ANI=@CallerDN, @DNIS=@CalledDN, @PremCust='0', @CustAcct='', @CustName='', @Contact='', @SalesRep=''

--FIRST CHECK INCOMING UUI
-------------------------------------------------------------------------------------
--print 'UUI='+@UUI+'!!!!'
select @UUIOriginal=@UUI
if (@UUI is null or len(@UUI)=0)
	begin
		select @AvayaDNIS=''
	end
else
	begin
		--We must have a UUI , so split it out into the variables.
		if (charindex(',', @UUI)=0)
			begin
				--we have 1 element - which will have to be avayaDNIS and ANI
				select @AvayaDNIS=substring(@UUI,1,4)
				if len(@UUI)>4 
					begin
						select @ANI=substring(@UUI,1,5)
					end
			end
		else
			begin
				--print @UUI
				While (charindex(',',@UUI)>0)
					begin
						set @CurrVal=substring(@UUI,1,CHARINDEX(',',@UUI,1)-1)  
						set @UUI = substring(@UUI,charindex(',',@UUI,1)+1, len(@UUI) )
						if @UUIElem=0
							begin
								select @AvayaDNIS=substring(@CurrVal,1,4)
								if len(@CurrVal)>4 
									begin
										select @ANI=substring(@CurrVal,5,15)
									end		
							end

						if @UUIElem=1
							begin
								select @DNIS=@CurrVal
							end 
								
						if @UUIElem=2
							begin
								select @PremCust=@CurrVal
								if (not (@PremCust='0' or @PremCust='1'))
									begin
										select @PremCust='0'
									end
							end 

						if @UUIelem=3
							begin
								select @CustAcct=@CurrVal
							end
						if @UUIelem=4
							begin
								select @CustName=@CurrVal
							end	
						if @UUIelem=5
							begin
								select @Contact=@CurrVal
							end
						if @UUIelem=6
							begin
								select @SalesRep=@CurrVal
							end


						set @UUIElem=@UUIElem+1
					end

			end

	end
-------------------------------------------------------------------------------------
-- END of incoming UUI Parsing


-- Check Action Route Process for VDN
-------------------------------------------------------------------------------------

select @ActionCode=0
select @ActionCode=coalesce(Action_code,0), @ActionRouteCompanyID=Convert(int,Cono), @VDNout=coalesce(VDN_out,'7000') --BKS added coalesce on 1/30/2008
from ActionRouteTable 
where VDN_in=@DNIS


--May need to add more processing here based on invalid returns
-------------------------------------------------------------------------------------
-- END of Check Action Route Process for VDN


--Routing Performed Here
-------------------------------------------------------------------------------------
select @DEST=@VDNOut
--if @ActionCode=0
--	begin
--		select @DEST=@VDNout
--	end

--Get Customer Data
if (@ActionCode=1 or @ActionCode=2 or @ActionCode=3 or @ActionCode=4)
	begin
		DECLARE @hold TABLE
		(
		  PremCust bit null, 
		  AcctTemp varchar(15) null,
		  CustName varchar(30) null,
		  SalesRepTemp varchar(6) null,
		  Contact varchar(29) null
		)

		select @count=(select count(coalesce(uni.premium_customer,0))
		from datahub..phone p inner join datahub..contact c on p.contact_id=c.id
		inner join datahub..customer cust on c.customer_id=cust.id
		inner join datahub..unidata_customer uc on cust.id=uc.customer_id
		inner join datahub..unidata_no uni on uc.unidata_no_id=uni.id
		where p.domestic_number=@CallerDN),
		@CompanyCount=(
			select count(coalesce(uni.premium_customer,0))
			from datahub..phone p inner join datahub..contact c on p.contact_id=c.id
			inner join datahub..customer cust on c.customer_id=cust.id
			inner join datahub..unidata_customer uc on cust.id=uc.customer_id
			inner join datahub..unidata_no uni on uc.unidata_no_id=uni.id
			where p.domestic_number=@CallerDN
			and Convert(int,uni.cono)=@ActionRouteCompanyID
		)

		--set processing flag
		select @ProcessPath=0 -- do nothing really, set variables accordingly
		-- options
		--	0 = nothing
		--	10 - single record found, within the routing company
		--	11 - single record found, NOT within the routing company
		--	100 - many records found - within the routing company
		--	110 - many records found, NONE within the routing company

		--Check the main count
		if (@count<1 or @count>1)
			begin
				if @Count=0
					begin
						select @ProcessPath=0
					end
				else
					begin
						if (@CompanyCount>0)
							begin
								select @ProcessPath=100 --set flag to process multiples for the routing company
							end
						else
							
							begin
								select @ProcessPath=110 -- we have multiples, not in the routing company though
							end
					end

			end
		else
			begin
				if (@CompanyCount>0)
					begin
						select @ProcessPath=10 --we have single -in routing company
					end	
				else
					begin
						select @ProcessPath=11 -- we have single - NOT in routing company
					end
			end
		--print @ProcessPath
		--Handle the processing flow
		if (@ProcessPath=0)
			begin
			    --print 'setting defaults'
				select @PremCust=0, @AcctTemp='', @CustName='', @SalesRepTemp='',@Contact='', @CompanyID=0, @SalesRep='', @CSRTemp=''
			end

		if (@ProcessPath=10)
			begin
				--print 'there is 1 record, so get it
				select  @PremCust=coalesce(h.premcust,0), @AcctTemp=coalesce(h.accttemp,'000000000'), @CustName=coalesce(substring( h.custname,1,30),'Unknown Customer'), @SalesRepTemp=coalesce(h.salesreptemp,''),@Contact=coalesce(h.contact,''), @CompanyID=h.CompanyID, @CSRTemp=coalesce(h.CSRTemp,'')
				from 
				(
					select coalesce(uni.premium_customer,0) as PremCust, coalesce(uni.unidata_id,'000000000') as AcctTemp, coalesce(substring( cust.cust_name,1,30),'Unknown Customer') as CustName, coalesce(uni.insidesalesrepcode,'') as SalesRepTemp, coalesce(c.first_name+' '+c.last_name, '') as Contact, Convert(int,coalesce(uni.cono,'0')) as CompanyID, coalesce(uni.CSRRepCode,'') as CSRTemp
					from datahub..phone p inner join datahub..contact c on p.contact_id=c.id
					inner join datahub..customer cust on c.customer_id=cust.id
					inner join datahub..unidata_customer uc on cust.id=uc.customer_id
					inner join datahub..unidata_no uni on uc.unidata_no_id=uni.id
					where p.domestic_number=@CallerDN
					and convert(int,uni.cono)=@ActionRouteCompanyID
				) h
			end

		if (@ProcessPath=11 or @ProcessPath=110) --one or more - but none in the action route company
			begin
				--print 'there is 1 record, so get it
				--SalesRepTemp will be returned as ''
				select  @PremCust=coalesce(h.premcust,0), @AcctTemp=coalesce(h.accttemp,'000000000'), @CustName=coalesce(substring( h.custname,1,30),'Unknown Customer'), @SalesRepTemp=coalesce(h.salesreptemp,''),@Contact=coalesce(h.contact,''), @CompanyID=h.CompanyID, @CSRTemp=coalesce(h.CSRTemp,'')
				from 
				(
					select coalesce(uni.premium_customer,0) as PremCust, coalesce(uni.unidata_id,'000000000') as AcctTemp, coalesce(substring( cust.cust_name,1,30),'Unknown Customer') as CustName, '' as SalesRepTemp, '' as CSRTemp, coalesce(c.first_name+' '+c.last_name, '') as Contact, Convert(int,coalesce(uni.cono,'0')) as CompanyID
					from datahub..phone p inner join datahub..contact c on p.contact_id=c.id
					inner join datahub..customer cust on c.customer_id=cust.id
					inner join datahub..unidata_customer uc on cust.id=uc.customer_id
					inner join datahub..unidata_no uni on uc.unidata_no_id=uni.id
					where p.domestic_number=@CallerDN
				) h
			end

		if @ProcessPath=100 -- many records, 1 or more in action route company
			begin
				--print 'getting first 1'
				select @PremCust=coalesce(h.premcust,0), @AcctTemp=coalesce(h.accttemp,'000000000'), @CustName=coalesce(substring( h.custname,1,30),'Unknown Customer'), @SalesRepTemp=coalesce(h.salesreptemp,''), @CompanyID=h.CompanyID, @Contact=coalesce(h.contact,''),@CSRTemp=coalesce(h.CSRTemp,'')
				from 
					(
						select top 1 coalesce(uni.premium_customer,0) as PremCust, coalesce(uni.unidata_id,'000000000') as AcctTemp, coalesce(substring( cust.cust_name,1,30),'Unknown Customer') as CustName, coalesce(uni.insidesalesrepcode,'') as SalesRepTemp, coalesce(uni.CSRRepCode,'') as CSRTemp, coalesce(c.first_name+' '+c.last_name, '') as Contact, Convert(int,coalesce(uni.cono,'0')) as CompanyID
						from datahub..phone p inner join datahub..contact c on p.contact_id=c.id
						inner join datahub..customer cust on c.customer_id=cust.id
						inner join datahub..unidata_customer uc on cust.id=uc.customer_id
						inner join datahub..unidata_no uni on uc.unidata_no_id=uni.id
						where p.domestic_number=@CallerDN
						and convert(int,uni.cono)=@ActionRouteCompanyID
					) h 
			end
	



	end





-- Get Agent Data
--BKS 10/09/2008
if @ActionCode=2 or @ActionCode=3 or @ActionCode=4 --new 05/06/2008 - for Brooks - added ActionCode3 to this processing
	begin
		-- simply grab it from the query above
		--BKS 10/09/2008 for Option #4
		--print @ActionCode
		if @ActionCode=4
			begin
				if (@CSRTemp is null) or (coalesce(@CSRTemp,'')='')
					begin
						select @CSRTemp=@SalesRepTemp
					end
				select @SalesRep=@CSRTemp
				--print @SalesRep
			end
		else
			begin
				select @SalesRep=@SalesRepTemp
--print @SalesRep
			end
		-- End of BKS 10/09/2008 edit
		
		if len(@SalesRep)>0
			begin
				--print @SalesRep
				select @SalesRepTemp=''

				-- BKS added 2008/05/01
				--Do we need to perform an override?
				select @Override=''
				select @Override=OverrideSalesRepcode from AvayaSalesRepOverride where SalesRepCode=@SalesRep
				--print 'override is : ' + @Override
				if len(@Override)>=4
					begin
						select @SalesRep=@Override
					end


				--BKS 04/01/2008 - insert Avaya ACS DB Lookup here.
				---------------------------------------------------
				--select @SalesRepTemp=(select c.ConfigData from AvayaACSDB.ACS.dbo.tblUserFilter uf left outer join AvayaACSDB.ACS.dbo.tblConfigPrimary c on uf.UserID=c.UserID	left outer join AvayaACSDB.ACS.dbo.tblUser u on uf.UserID=u.UserID	where uf.FilterValue=@SalesRep and uf.FilterID=2 and c.TemplateID=390)
				----SELECT @SalesRepTemp= (select top 1 * FROM OPENQUERY(AvayaACSDB, @Sql))
				----IF nothing is returned then lets query Again for Chief with 'C' appended to front
				--if (@SalesRepTemp is null) or (@SalesRepTemp='')
				--	begin
				--		select @SalesRepTemp=(select c.ConfigData from AvayaACSDB.ACS.dbo.tblUserFilter uf left outer join AvayaACSDB.ACS.dbo.tblConfigPrimary c on uf.UserID=c.UserID	left outer join AvayaACSDB.ACS.dbo.tblUser u on uf.UserID=u.UserID	where uf.FilterValue='C' + @SalesRep and uf.FilterID=2 and c.TemplateID=390)
				--	end
				--BKS 04/07/2016 - remove reference to old ACSDB and use AvayaUserAgentID table on BHCCRM
				Select @SalesRepTemp=(Select coalesce(AgentID,'') from AvayaUserAgentID where UserLogin=@SalesRep)
				---------------------------------------------------
				-- BKS 04/01/2008 - OLD CODE select @SalesRepTemp=coalesce(phoneextension,'') from SecurityUsers where SalesRepCode=@SalesRep
				if len(@SalesRepTemp)>=4
					begin				
						select @DEST=@SalesRepTemp
					end 
			end
	end

--Get Agent Data based on area code
--BKS 05/06/2008 Modified for Brooks
if @ActionCode=3
	begin
		--BKS 05/06/2008 - add check, do we have a sales rep?
		-- if we have a sales rep, go to sales rep
		-- if not, look up in the AreaCodeRepCode table
		-- if nothing there, go to default @VDNout

		if len(@SalesRep)>0
			begin
				--now we simply need to check the VDN that was determined from ActionRoute #2 up above
				if len(@SalesRepTemp)>=4 
					begin
						select @DEST=@SalesRepTemp
					end
				else
					begin
						--we will reset @SalesRep to '', and have it flow through the area code lookup
						Select @SalesRep=''
					end
			end 
		-- BKS added on 1/20/2009
		select @SalesRep=''

		if len(@SalesRep)=0
			begin
				--print 'getting rep based on area code for '
				if isnumeric(substring(@CallerDN,1,3))=1
					begin

						select @SalesRep=coalesce(acrc.SalesRepCode,'') from AreaCodeRepCode acrc where AreaCode=substring(@CallerDN,1,3) and CompanyID=@ActionRouteCompanyID --BKS added CompanyID clause on 5/1/2013
						if len(@SalesRep)>0
							begin
								--print @SalesRep
								select @SalesRepTemp=''
								--select @SalesRepTemp=(select c.ConfigData from AvayaACSDB.ACS.dbo.tblUserFilter uf left outer join AvayaACSDB.ACS.dbo.tblConfigPrimary c on uf.UserID=c.UserID	left outer join AvayaACSDB.ACS.dbo.tblUser u on uf.UserID=u.UserID	where uf.FilterValue=@SalesRep and uf.FilterID=2 and c.TemplateID=390)
								--if (@SalesRepTemp is null) or (@SalesRepTemp='')
								--	begin
								--		select @SalesRepTemp=(select c.ConfigData from AvayaACSDB.ACS.dbo.tblUserFilter uf left outer join AvayaACSDB.ACS.dbo.tblConfigPrimary c on uf.UserID=c.UserID	left outer join AvayaACSDB.ACS.dbo.tblUser u on uf.UserID=u.UserID	where uf.FilterValue='C' + @SalesRep and uf.FilterID=2 and c.TemplateID=390)
								--	end
								--select @SalesRepTemp=coalesce(phoneextension,'') from SecurityUsers where SalesRepCode=@SalesRep
								--BKS 04/07/2016 - remove reference to old ACSDB and use AvayaUserAgentID table on BHCCRM
								Select @SalesRepTemp=(Select coalesce(AgentID,'') from AvayaUserAgentID where UserLogin=@SalesRep)
				
								if len(@SalesRepTemp)>=4
									begin
							
										select @DEST=@SalesRepTemp
									end 
							end

					end	
			end 

		--One last check
		if (@Dest is null) or (len(@Dest)=0)
			begin
				select @Dest=@VDNOut
			end
	end


-------------------------------------------------------------------------------------
--END Routing


--ASSEMBLE UUI
--BKS modified on 02/15/2008 - replaced @CustAcct with @AcctTemp
--BKS added per Fred 03/10/2008
--==============================================================
--print 'AvayaDNIS:' + @AvayaDNIS+'!!!!'
if (@AvayaDNIS='    ' or len(@AvayaDNIS)=0)
	begin
		select @AvayaDNIS=VDN_Out from ActionRouteTable where VDN_In=@DNIS
	end
--==============================================================
-------------------------------------------------------------------------------------
if @ProcessPath=11 or @ProcessPath=110
--Clear out UUI data because we don't want non company accounts to appear in CCE
	begin
		select @PremCust='0'
		select @AcctTemp=''
		select @CustName=''
		select @Contact=''
	end


select @UUI=@AvayaDNIS+@ANI+','+@DNIS+','+@PremCust+','+@AcctTemp+','+@CustName+','+@Contact+','+@SalesRep
-------------------------------------------------------------------------------------
--END ASSEMBLE UUI


if @DEST is null
	begin
		select @DEST='5793'
	end 

--BKS call routing change for Brooks - if 3411, route to 5720
if @ActionRouteCompanyID=1 and @DEST='3411'
	begin
		select @DEST='5720'
	end

--END BKS call routing change for Brooks
-- FINAL RESULT to AVAYA
--LogIT
--insert into ActionRouteLog (LogDate, CallerDN, CalledDN, CollectedDigits, UUI, UUIOut, Dest)
--values (getdate(),@CallerDN, @CalledDN, @CollectedDigits, coalesce(@UUIOriginal,''), @UUI, @Dest)

Select @UUI as UUI, @DEST as DEST

--New way requested per Fred
--Select UUI=@UUI, DEST=@DEST
--Select UUI = 'XXXX9999999999,5745,0,,CUSOMER NAME HERE,CONTACT NAME HERE,SALES REP ID HERE', DEST = '6000'


end




GO