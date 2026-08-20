-- 0123_GeoCitySeedData.sql
-- Seeds Location.GeoCity and Location.GeoPostalCode with US city reference data so the
-- city typeahead works before any provider-learned rows exist. Idempotent (MERGE).

MERGE Location.GeoCity AS target
USING (VALUES
	(N'US', N'AL', N'Birmingham', N'Jefferson', N'35203'), (N'US', N'AL', N'Montgomery', N'Montgomery', N'36104'), (N'US', N'AL', N'Huntsville', N'Madison', N'35801'), (N'US', N'AL', N'Mobile', N'Mobile', N'36602'),
	(N'US', N'AK', N'Anchorage', N'Anchorage', N'99501'), (N'US', N'AK', N'Fairbanks', N'Fairbanks North Star', N'99701'), (N'US', N'AK', N'Juneau', N'Juneau', N'99801'),
	(N'US', N'AZ', N'Phoenix', N'Maricopa', N'85001'), (N'US', N'AZ', N'Tucson', N'Pima', N'85701'), (N'US', N'AZ', N'Mesa', N'Maricopa', N'85201'), (N'US', N'AZ', N'Scottsdale', N'Maricopa', N'85251'), (N'US', N'AZ', N'Chandler', N'Maricopa', N'85225'),
	(N'US', N'AR', N'Little Rock', N'Pulaski', N'72201'), (N'US', N'AR', N'Fayetteville', N'Washington', N'72701'), (N'US', N'AR', N'Fort Smith', N'Sebastian', N'72901'),
	(N'US', N'CA', N'Los Angeles', N'Los Angeles', N'90001'), (N'US', N'CA', N'San Diego', N'San Diego', N'92101'), (N'US', N'CA', N'San Jose', N'Santa Clara', N'95101'), (N'US', N'CA', N'San Francisco', N'San Francisco', N'94102'), (N'US', N'CA', N'Fresno', N'Fresno', N'93701'), (N'US', N'CA', N'Sacramento', N'Sacramento', N'95814'), (N'US', N'CA', N'Long Beach', N'Los Angeles', N'90802'), (N'US', N'CA', N'Oakland', N'Alameda', N'94601'), (N'US', N'CA', N'Bakersfield', N'Kern', N'93301'), (N'US', N'CA', N'Anaheim', N'Orange', N'92801'), (N'US', N'CA', N'Irvine', N'Orange', N'92602'), (N'US', N'CA', N'Riverside', N'Riverside', N'92501'),
	(N'US', N'CO', N'Denver', N'Denver', N'80202'), (N'US', N'CO', N'Colorado Springs', N'El Paso', N'80903'), (N'US', N'CO', N'Aurora', N'Arapahoe', N'80010'), (N'US', N'CO', N'Fort Collins', N'Larimer', N'80521'), (N'US', N'CO', N'Boulder', N'Boulder', N'80302'),
	(N'US', N'CT', N'Bridgeport', N'Fairfield', N'06604'), (N'US', N'CT', N'New Haven', N'New Haven', N'06510'), (N'US', N'CT', N'Hartford', N'Hartford', N'06103'), (N'US', N'CT', N'Stamford', N'Fairfield', N'06901'),
	(N'US', N'DE', N'Wilmington', N'New Castle', N'19801'), (N'US', N'DE', N'Dover', N'Kent', N'19901'), (N'US', N'DE', N'Newark', N'New Castle', N'19711'),
	(N'US', N'DC', N'Washington', N'District of Columbia', N'20001'),
	(N'US', N'FL', N'Jacksonville', N'Duval', N'32202'), (N'US', N'FL', N'Miami', N'Miami-Dade', N'33101'), (N'US', N'FL', N'Tampa', N'Hillsborough', N'33602'), (N'US', N'FL', N'Orlando', N'Orange', N'32801'), (N'US', N'FL', N'St. Petersburg', N'Pinellas', N'33701'), (N'US', N'FL', N'Fort Lauderdale', N'Broward', N'33301'), (N'US', N'FL', N'Tallahassee', N'Leon', N'32301'), (N'US', N'FL', N'Sarasota', N'Sarasota', N'34236'),
	(N'US', N'GA', N'Atlanta', N'Fulton', N'30303'), (N'US', N'GA', N'Savannah', N'Chatham', N'31401'), (N'US', N'GA', N'Augusta', N'Richmond', N'30901'), (N'US', N'GA', N'Columbus', N'Muscogee', N'31901'), (N'US', N'GA', N'Macon', N'Bibb', N'31201'),
	(N'US', N'HI', N'Honolulu', N'Honolulu', N'96813'), (N'US', N'HI', N'Hilo', N'Hawaii', N'96720'), (N'US', N'HI', N'Kailua', N'Honolulu', N'96734'),
	(N'US', N'ID', N'Boise', N'Ada', N'83702'), (N'US', N'ID', N'Meridian', N'Ada', N'83642'), (N'US', N'ID', N'Idaho Falls', N'Bonneville', N'83402'),
	(N'US', N'IL', N'Chicago', N'Cook', N'60601'), (N'US', N'IL', N'Aurora', N'Kane', N'60505'), (N'US', N'IL', N'Naperville', N'DuPage', N'60540'), (N'US', N'IL', N'Springfield', N'Sangamon', N'62701'), (N'US', N'IL', N'Rockford', N'Winnebago', N'61101'), (N'US', N'IL', N'Peoria', N'Peoria', N'61602'),
	(N'US', N'IN', N'Indianapolis', N'Marion', N'46204'), (N'US', N'IN', N'Fort Wayne', N'Allen', N'46802'), (N'US', N'IN', N'Evansville', N'Vanderburgh', N'47708'), (N'US', N'IN', N'South Bend', N'St. Joseph', N'46601'),
	(N'US', N'IA', N'Des Moines', N'Polk', N'50309'), (N'US', N'IA', N'Cedar Rapids', N'Linn', N'52401'), (N'US', N'IA', N'Davenport', N'Scott', N'52801'),
	(N'US', N'KS', N'Wichita', N'Sedgwick', N'67202'), (N'US', N'KS', N'Overland Park', N'Johnson', N'66204'), (N'US', N'KS', N'Kansas City', N'Wyandotte', N'66101'), (N'US', N'KS', N'Topeka', N'Shawnee', N'66603'),
	(N'US', N'KY', N'Louisville', N'Jefferson', N'40202'), (N'US', N'KY', N'Lexington', N'Fayette', N'40507'), (N'US', N'KY', N'Bowling Green', N'Warren', N'42101'),
	(N'US', N'LA', N'New Orleans', N'Orleans', N'70112'), (N'US', N'LA', N'Baton Rouge', N'East Baton Rouge', N'70801'), (N'US', N'LA', N'Shreveport', N'Caddo', N'71101'), (N'US', N'LA', N'Lafayette', N'Lafayette', N'70501'),
	(N'US', N'ME', N'Portland', N'Cumberland', N'04101'), (N'US', N'ME', N'Lewiston', N'Androscoggin', N'04240'), (N'US', N'ME', N'Bangor', N'Penobscot', N'04401'),
	(N'US', N'MD', N'Baltimore', N'Baltimore City', N'21201'), (N'US', N'MD', N'Columbia', N'Howard', N'21044'), (N'US', N'MD', N'Annapolis', N'Anne Arundel', N'21401'), (N'US', N'MD', N'Frederick', N'Frederick', N'21701'),
	(N'US', N'MA', N'Boston', N'Suffolk', N'02108'), (N'US', N'MA', N'Worcester', N'Worcester', N'01608'), (N'US', N'MA', N'Springfield', N'Hampden', N'01103'), (N'US', N'MA', N'Cambridge', N'Middlesex', N'02138'), (N'US', N'MA', N'Lowell', N'Middlesex', N'01852'),
	(N'US', N'MI', N'Detroit', N'Wayne', N'48226'), (N'US', N'MI', N'Grand Rapids', N'Kent', N'49503'), (N'US', N'MI', N'Warren', N'Macomb', N'48088'), (N'US', N'MI', N'Ann Arbor', N'Washtenaw', N'48104'), (N'US', N'MI', N'Lansing', N'Ingham', N'48933'),
	(N'US', N'MN', N'Minneapolis', N'Hennepin', N'55401'), (N'US', N'MN', N'Saint Paul', N'Ramsey', N'55101'), (N'US', N'MN', N'Rochester', N'Olmsted', N'55901'), (N'US', N'MN', N'Duluth', N'St. Louis', N'55802'),
	(N'US', N'MS', N'Jackson', N'Hinds', N'39201'), (N'US', N'MS', N'Gulfport', N'Harrison', N'39501'), (N'US', N'MS', N'Hattiesburg', N'Forrest', N'39401'),
	(N'US', N'MO', N'Kansas City', N'Jackson', N'64106'), (N'US', N'MO', N'Saint Louis', N'St. Louis City', N'63101'), (N'US', N'MO', N'Springfield', N'Greene', N'65806'), (N'US', N'MO', N'Columbia', N'Boone', N'65201'),
	(N'US', N'MT', N'Billings', N'Yellowstone', N'59101'), (N'US', N'MT', N'Missoula', N'Missoula', N'59801'), (N'US', N'MT', N'Bozeman', N'Gallatin', N'59715'), (N'US', N'MT', N'Helena', N'Lewis and Clark', N'59601'),
	(N'US', N'NE', N'Omaha', N'Douglas', N'68102'), (N'US', N'NE', N'Lincoln', N'Lancaster', N'68508'), (N'US', N'NE', N'Bellevue', N'Sarpy', N'68005'),
	(N'US', N'NV', N'Las Vegas', N'Clark', N'89101'), (N'US', N'NV', N'Henderson', N'Clark', N'89011'), (N'US', N'NV', N'Reno', N'Washoe', N'89501'), (N'US', N'NV', N'Carson City', N'Carson City', N'89701'),
	(N'US', N'NH', N'Manchester', N'Hillsborough', N'03101'), (N'US', N'NH', N'Nashua', N'Hillsborough', N'03060'), (N'US', N'NH', N'Concord', N'Merrimack', N'03301'),
	(N'US', N'NJ', N'Newark', N'Essex', N'07102'), (N'US', N'NJ', N'Jersey City', N'Hudson', N'07302'), (N'US', N'NJ', N'Paterson', N'Passaic', N'07501'), (N'US', N'NJ', N'Trenton', N'Mercer', N'08608'), (N'US', N'NJ', N'Edison', N'Middlesex', N'08817'),
	(N'US', N'NM', N'Albuquerque', N'Bernalillo', N'87102'), (N'US', N'NM', N'Las Cruces', N'Dona Ana', N'88001'), (N'US', N'NM', N'Santa Fe', N'Santa Fe', N'87501'),
	(N'US', N'NY', N'New York', N'New York', N'10001'), (N'US', N'NY', N'Buffalo', N'Erie', N'14202'), (N'US', N'NY', N'Rochester', N'Monroe', N'14604'), (N'US', N'NY', N'Yonkers', N'Westchester', N'10701'), (N'US', N'NY', N'Syracuse', N'Onondaga', N'13202'), (N'US', N'NY', N'Albany', N'Albany', N'12207'), (N'US', N'NY', N'Brooklyn', N'Kings', N'11201'),
	(N'US', N'NC', N'Charlotte', N'Mecklenburg', N'28202'), (N'US', N'NC', N'Raleigh', N'Wake', N'27601'), (N'US', N'NC', N'Greensboro', N'Guilford', N'27401'), (N'US', N'NC', N'Durham', N'Durham', N'27701'), (N'US', N'NC', N'Winston-Salem', N'Forsyth', N'27101'), (N'US', N'NC', N'Asheville', N'Buncombe', N'28801'),
	(N'US', N'ND', N'Fargo', N'Cass', N'58102'), (N'US', N'ND', N'Bismarck', N'Burleigh', N'58501'), (N'US', N'ND', N'Grand Forks', N'Grand Forks', N'58201'),
	(N'US', N'OH', N'Columbus', N'Franklin', N'43215'), (N'US', N'OH', N'Cleveland', N'Cuyahoga', N'44113'), (N'US', N'OH', N'Cincinnati', N'Hamilton', N'45202'), (N'US', N'OH', N'Toledo', N'Lucas', N'43604'), (N'US', N'OH', N'Akron', N'Summit', N'44308'), (N'US', N'OH', N'Dayton', N'Montgomery', N'45402'),
	(N'US', N'OK', N'Oklahoma City', N'Oklahoma', N'73102'), (N'US', N'OK', N'Tulsa', N'Tulsa', N'74103'), (N'US', N'OK', N'Norman', N'Cleveland', N'73069'),
	(N'US', N'OR', N'Portland', N'Multnomah', N'97201'), (N'US', N'OR', N'Salem', N'Marion', N'97301'), (N'US', N'OR', N'Eugene', N'Lane', N'97401'), (N'US', N'OR', N'Bend', N'Deschutes', N'97701'),
	(N'US', N'PA', N'Philadelphia', N'Philadelphia', N'19102'), (N'US', N'PA', N'Pittsburgh', N'Allegheny', N'15222'), (N'US', N'PA', N'Allentown', N'Lehigh', N'18101'), (N'US', N'PA', N'Erie', N'Erie', N'16501'), (N'US', N'PA', N'Harrisburg', N'Dauphin', N'17101'),
	(N'US', N'RI', N'Providence', N'Providence', N'02903'), (N'US', N'RI', N'Warwick', N'Kent', N'02886'), (N'US', N'RI', N'Cranston', N'Providence', N'02920'),
	(N'US', N'SC', N'Charleston', N'Charleston', N'29401'), (N'US', N'SC', N'Columbia', N'Richland', N'29201'), (N'US', N'SC', N'Greenville', N'Greenville', N'29601'), (N'US', N'SC', N'Myrtle Beach', N'Horry', N'29577'),
	(N'US', N'SD', N'Sioux Falls', N'Minnehaha', N'57104'), (N'US', N'SD', N'Rapid City', N'Pennington', N'57701'), (N'US', N'SD', N'Pierre', N'Hughes', N'57501'),
	(N'US', N'TN', N'Nashville', N'Davidson', N'37201'), (N'US', N'TN', N'Memphis', N'Shelby', N'38103'), (N'US', N'TN', N'Knoxville', N'Knox', N'37902'), (N'US', N'TN', N'Chattanooga', N'Hamilton', N'37402'),
	(N'US', N'TX', N'Houston', N'Harris', N'77002'), (N'US', N'TX', N'San Antonio', N'Bexar', N'78205'), (N'US', N'TX', N'Dallas', N'Dallas', N'75201'), (N'US', N'TX', N'Austin', N'Travis', N'78701'), (N'US', N'TX', N'Fort Worth', N'Tarrant', N'76102'), (N'US', N'TX', N'El Paso', N'El Paso', N'79901'), (N'US', N'TX', N'Arlington', N'Tarrant', N'76010'), (N'US', N'TX', N'Corpus Christi', N'Nueces', N'78401'), (N'US', N'TX', N'Plano', N'Collin', N'75074'), (N'US', N'TX', N'Lubbock', N'Lubbock', N'79401'),
	(N'US', N'UT', N'Salt Lake City', N'Salt Lake', N'84101'), (N'US', N'UT', N'West Valley City', N'Salt Lake', N'84119'), (N'US', N'UT', N'Provo', N'Utah', N'84601'), (N'US', N'UT', N'Ogden', N'Weber', N'84401'),
	(N'US', N'VT', N'Burlington', N'Chittenden', N'05401'), (N'US', N'VT', N'Montpelier', N'Washington', N'05602'), (N'US', N'VT', N'Rutland', N'Rutland', N'05701'),
	(N'US', N'VA', N'Virginia Beach', N'Virginia Beach City', N'23450'), (N'US', N'VA', N'Norfolk', N'Norfolk City', N'23510'), (N'US', N'VA', N'Richmond', N'Richmond City', N'23219'), (N'US', N'VA', N'Arlington', N'Arlington', N'22201'), (N'US', N'VA', N'Alexandria', N'Alexandria City', N'22301'),
	(N'US', N'WA', N'Seattle', N'King', N'98101'), (N'US', N'WA', N'Spokane', N'Spokane', N'99201'), (N'US', N'WA', N'Tacoma', N'Pierce', N'98402'), (N'US', N'WA', N'Vancouver', N'Clark', N'98660'), (N'US', N'WA', N'Bellevue', N'King', N'98004'), (N'US', N'WA', N'Olympia', N'Thurston', N'98501'),
	(N'US', N'WV', N'Charleston', N'Kanawha', N'25301'), (N'US', N'WV', N'Huntington', N'Cabell', N'25701'), (N'US', N'WV', N'Morgantown', N'Monongalia', N'26501'),
	(N'US', N'WI', N'Milwaukee', N'Milwaukee', N'53202'), (N'US', N'WI', N'Madison', N'Dane', N'53703'), (N'US', N'WI', N'Green Bay', N'Brown', N'54301'), (N'US', N'WI', N'Kenosha', N'Kenosha', N'53140'),
	(N'US', N'WY', N'Cheyenne', N'Laramie', N'82001'), (N'US', N'WY', N'Casper', N'Natrona', N'82601'), (N'US', N'WY', N'Jackson', N'Teton', N'83001'),
	(N'US', N'PR', N'San Juan', N'San Juan', N'00901'), (N'US', N'GU', N'Hagatna', N'Hagatna', N'96910'), (N'US', N'VI', N'Charlotte Amalie', N'St. Thomas', N'00802')
) AS source (CountryCode, StateCode, CityName, County, PrimaryPostalCode)
ON target.CountryCode = source.CountryCode AND target.StateCode = source.StateCode AND target.CityName = source.CityName
WHEN NOT MATCHED THEN
	INSERT (GeoCityId, CountryCode, StateCode, CityName, County, SourceCode)
	VALUES (NEWID(), source.CountryCode, source.StateCode, source.CityName, source.County, N'Seed')
WHEN MATCHED AND target.County IS NULL AND source.County IS NOT NULL THEN
	UPDATE SET County = source.County, ModifiedDateUtc = SYSUTCDATETIME();
GO

-- Seed the primary postal code per seeded city (idempotent).
;WITH SeedZips AS (
	SELECT city.GeoCityId, source.PrimaryPostalCode
	FROM (VALUES
		(N'US', N'AL', N'Birmingham', N'35203'), (N'US', N'AL', N'Montgomery', N'36104'), (N'US', N'AL', N'Huntsville', N'35801'), (N'US', N'AL', N'Mobile', N'36602'),
		(N'US', N'AK', N'Anchorage', N'99501'), (N'US', N'AK', N'Fairbanks', N'99701'), (N'US', N'AK', N'Juneau', N'99801'),
		(N'US', N'AZ', N'Phoenix', N'85001'), (N'US', N'AZ', N'Tucson', N'85701'), (N'US', N'AZ', N'Mesa', N'85201'), (N'US', N'AZ', N'Scottsdale', N'85251'), (N'US', N'AZ', N'Chandler', N'85225'),
		(N'US', N'AR', N'Little Rock', N'72201'), (N'US', N'AR', N'Fayetteville', N'72701'), (N'US', N'AR', N'Fort Smith', N'72901'),
		(N'US', N'CA', N'Los Angeles', N'90001'), (N'US', N'CA', N'San Diego', N'92101'), (N'US', N'CA', N'San Jose', N'95101'), (N'US', N'CA', N'San Francisco', N'94102'), (N'US', N'CA', N'Fresno', N'93701'), (N'US', N'CA', N'Sacramento', N'95814'), (N'US', N'CA', N'Long Beach', N'90802'), (N'US', N'CA', N'Oakland', N'94601'), (N'US', N'CA', N'Bakersfield', N'93301'), (N'US', N'CA', N'Anaheim', N'92801'), (N'US', N'CA', N'Irvine', N'92602'), (N'US', N'CA', N'Riverside', N'92501'),
		(N'US', N'CO', N'Denver', N'80202'), (N'US', N'CO', N'Colorado Springs', N'80903'), (N'US', N'CO', N'Aurora', N'80010'), (N'US', N'CO', N'Fort Collins', N'80521'), (N'US', N'CO', N'Boulder', N'80302'),
		(N'US', N'CT', N'Bridgeport', N'06604'), (N'US', N'CT', N'New Haven', N'06510'), (N'US', N'CT', N'Hartford', N'06103'), (N'US', N'CT', N'Stamford', N'06901'),
		(N'US', N'DE', N'Wilmington', N'19801'), (N'US', N'DE', N'Dover', N'19901'), (N'US', N'DE', N'Newark', N'19711'),
		(N'US', N'DC', N'Washington', N'20001'),
		(N'US', N'FL', N'Jacksonville', N'32202'), (N'US', N'FL', N'Miami', N'33101'), (N'US', N'FL', N'Tampa', N'33602'), (N'US', N'FL', N'Orlando', N'32801'), (N'US', N'FL', N'St. Petersburg', N'33701'), (N'US', N'FL', N'Fort Lauderdale', N'33301'), (N'US', N'FL', N'Tallahassee', N'32301'), (N'US', N'FL', N'Sarasota', N'34236'),
		(N'US', N'GA', N'Atlanta', N'30303'), (N'US', N'GA', N'Savannah', N'31401'), (N'US', N'GA', N'Augusta', N'30901'), (N'US', N'GA', N'Columbus', N'31901'), (N'US', N'GA', N'Macon', N'31201'),
		(N'US', N'HI', N'Honolulu', N'96813'), (N'US', N'HI', N'Hilo', N'96720'), (N'US', N'HI', N'Kailua', N'96734'),
		(N'US', N'ID', N'Boise', N'83702'), (N'US', N'ID', N'Meridian', N'83642'), (N'US', N'ID', N'Idaho Falls', N'83402'),
		(N'US', N'IL', N'Chicago', N'60601'), (N'US', N'IL', N'Aurora', N'60505'), (N'US', N'IL', N'Naperville', N'60540'), (N'US', N'IL', N'Springfield', N'62701'), (N'US', N'IL', N'Rockford', N'61101'), (N'US', N'IL', N'Peoria', N'61602'),
		(N'US', N'IN', N'Indianapolis', N'46204'), (N'US', N'IN', N'Fort Wayne', N'46802'), (N'US', N'IN', N'Evansville', N'47708'), (N'US', N'IN', N'South Bend', N'46601'),
		(N'US', N'IA', N'Des Moines', N'50309'), (N'US', N'IA', N'Cedar Rapids', N'52401'), (N'US', N'IA', N'Davenport', N'52801'),
		(N'US', N'KS', N'Wichita', N'67202'), (N'US', N'KS', N'Overland Park', N'66204'), (N'US', N'KS', N'Kansas City', N'66101'), (N'US', N'KS', N'Topeka', N'66603'),
		(N'US', N'KY', N'Louisville', N'40202'), (N'US', N'KY', N'Lexington', N'40507'), (N'US', N'KY', N'Bowling Green', N'42101'),
		(N'US', N'LA', N'New Orleans', N'70112'), (N'US', N'LA', N'Baton Rouge', N'70801'), (N'US', N'LA', N'Shreveport', N'71101'), (N'US', N'LA', N'Lafayette', N'70501'),
		(N'US', N'ME', N'Portland', N'04101'), (N'US', N'ME', N'Lewiston', N'04240'), (N'US', N'ME', N'Bangor', N'04401'),
		(N'US', N'MD', N'Baltimore', N'21201'), (N'US', N'MD', N'Columbia', N'21044'), (N'US', N'MD', N'Annapolis', N'21401'), (N'US', N'MD', N'Frederick', N'21701'),
		(N'US', N'MA', N'Boston', N'02108'), (N'US', N'MA', N'Worcester', N'01608'), (N'US', N'MA', N'Springfield', N'01103'), (N'US', N'MA', N'Cambridge', N'02138'), (N'US', N'MA', N'Lowell', N'01852'),
		(N'US', N'MI', N'Detroit', N'48226'), (N'US', N'MI', N'Grand Rapids', N'49503'), (N'US', N'MI', N'Warren', N'48088'), (N'US', N'MI', N'Ann Arbor', N'48104'), (N'US', N'MI', N'Lansing', N'48933'),
		(N'US', N'MN', N'Minneapolis', N'55401'), (N'US', N'MN', N'Saint Paul', N'55101'), (N'US', N'MN', N'Rochester', N'55901'), (N'US', N'MN', N'Duluth', N'55802'),
		(N'US', N'MS', N'Jackson', N'39201'), (N'US', N'MS', N'Gulfport', N'39501'), (N'US', N'MS', N'Hattiesburg', N'39401'),
		(N'US', N'MO', N'Kansas City', N'64106'), (N'US', N'MO', N'Saint Louis', N'63101'), (N'US', N'MO', N'Springfield', N'65806'), (N'US', N'MO', N'Columbia', N'65201'),
		(N'US', N'MT', N'Billings', N'59101'), (N'US', N'MT', N'Missoula', N'59801'), (N'US', N'MT', N'Bozeman', N'59715'), (N'US', N'MT', N'Helena', N'59601'),
		(N'US', N'NE', N'Omaha', N'68102'), (N'US', N'NE', N'Lincoln', N'68508'), (N'US', N'NE', N'Bellevue', N'68005'),
		(N'US', N'NV', N'Las Vegas', N'89101'), (N'US', N'NV', N'Henderson', N'89011'), (N'US', N'NV', N'Reno', N'89501'), (N'US', N'NV', N'Carson City', N'89701'),
		(N'US', N'NH', N'Manchester', N'03101'), (N'US', N'NH', N'Nashua', N'03060'), (N'US', N'NH', N'Concord', N'03301'),
		(N'US', N'NJ', N'Newark', N'07102'), (N'US', N'NJ', N'Jersey City', N'07302'), (N'US', N'NJ', N'Paterson', N'07501'), (N'US', N'NJ', N'Trenton', N'08608'), (N'US', N'NJ', N'Edison', N'08817'),
		(N'US', N'NM', N'Albuquerque', N'87102'), (N'US', N'NM', N'Las Cruces', N'88001'), (N'US', N'NM', N'Santa Fe', N'87501'),
		(N'US', N'NY', N'New York', N'10001'), (N'US', N'NY', N'Buffalo', N'14202'), (N'US', N'NY', N'Rochester', N'14604'), (N'US', N'NY', N'Yonkers', N'10701'), (N'US', N'NY', N'Syracuse', N'13202'), (N'US', N'NY', N'Albany', N'12207'), (N'US', N'NY', N'Brooklyn', N'11201'),
		(N'US', N'NC', N'Charlotte', N'28202'), (N'US', N'NC', N'Raleigh', N'27601'), (N'US', N'NC', N'Greensboro', N'27401'), (N'US', N'NC', N'Durham', N'27701'), (N'US', N'NC', N'Winston-Salem', N'27101'), (N'US', N'NC', N'Asheville', N'28801'),
		(N'US', N'ND', N'Fargo', N'58102'), (N'US', N'ND', N'Bismarck', N'58501'), (N'US', N'ND', N'Grand Forks', N'58201'),
		(N'US', N'OH', N'Columbus', N'43215'), (N'US', N'OH', N'Cleveland', N'44113'), (N'US', N'OH', N'Cincinnati', N'45202'), (N'US', N'OH', N'Toledo', N'43604'), (N'US', N'OH', N'Akron', N'44308'), (N'US', N'OH', N'Dayton', N'45402'),
		(N'US', N'OK', N'Oklahoma City', N'73102'), (N'US', N'OK', N'Tulsa', N'74103'), (N'US', N'OK', N'Norman', N'73069'),
		(N'US', N'OR', N'Portland', N'97201'), (N'US', N'OR', N'Salem', N'97301'), (N'US', N'OR', N'Eugene', N'97401'), (N'US', N'OR', N'Bend', N'97701'),
		(N'US', N'PA', N'Philadelphia', N'19102'), (N'US', N'PA', N'Pittsburgh', N'15222'), (N'US', N'PA', N'Allentown', N'18101'), (N'US', N'PA', N'Erie', N'16501'), (N'US', N'PA', N'Harrisburg', N'17101'),
		(N'US', N'RI', N'Providence', N'02903'), (N'US', N'RI', N'Warwick', N'02886'), (N'US', N'RI', N'Cranston', N'02920'),
		(N'US', N'SC', N'Charleston', N'29401'), (N'US', N'SC', N'Columbia', N'29201'), (N'US', N'SC', N'Greenville', N'29601'), (N'US', N'SC', N'Myrtle Beach', N'29577'),
		(N'US', N'SD', N'Sioux Falls', N'57104'), (N'US', N'SD', N'Rapid City', N'57701'), (N'US', N'SD', N'Pierre', N'57501'),
		(N'US', N'TN', N'Nashville', N'37201'), (N'US', N'TN', N'Memphis', N'38103'), (N'US', N'TN', N'Knoxville', N'37902'), (N'US', N'TN', N'Chattanooga', N'37402'),
		(N'US', N'TX', N'Houston', N'77002'), (N'US', N'TX', N'San Antonio', N'78205'), (N'US', N'TX', N'Dallas', N'75201'), (N'US', N'TX', N'Austin', N'78701'), (N'US', N'TX', N'Fort Worth', N'76102'), (N'US', N'TX', N'El Paso', N'79901'), (N'US', N'TX', N'Arlington', N'76010'), (N'US', N'TX', N'Corpus Christi', N'78401'), (N'US', N'TX', N'Plano', N'75074'), (N'US', N'TX', N'Lubbock', N'79401'),
		(N'US', N'UT', N'Salt Lake City', N'84101'), (N'US', N'UT', N'West Valley City', N'84119'), (N'US', N'UT', N'Provo', N'84601'), (N'US', N'UT', N'Ogden', N'84401'),
		(N'US', N'VT', N'Burlington', N'05401'), (N'US', N'VT', N'Montpelier', N'05602'), (N'US', N'VT', N'Rutland', N'05701'),
		(N'US', N'VA', N'Virginia Beach', N'23450'), (N'US', N'VA', N'Norfolk', N'23510'), (N'US', N'VA', N'Richmond', N'23219'), (N'US', N'VA', N'Arlington', N'22201'), (N'US', N'VA', N'Alexandria', N'22301'),
		(N'US', N'WA', N'Seattle', N'98101'), (N'US', N'WA', N'Spokane', N'99201'), (N'US', N'WA', N'Tacoma', N'98402'), (N'US', N'WA', N'Vancouver', N'98660'), (N'US', N'WA', N'Bellevue', N'98004'), (N'US', N'WA', N'Olympia', N'98501'),
		(N'US', N'WV', N'Charleston', N'25301'), (N'US', N'WV', N'Huntington', N'25701'), (N'US', N'WV', N'Morgantown', N'26501'),
		(N'US', N'WI', N'Milwaukee', N'53202'), (N'US', N'WI', N'Madison', N'53703'), (N'US', N'WI', N'Green Bay', N'54301'), (N'US', N'WI', N'Kenosha', N'53140'),
		(N'US', N'WY', N'Cheyenne', N'82001'), (N'US', N'WY', N'Casper', N'82601'), (N'US', N'WY', N'Jackson', N'83001'),
		(N'US', N'PR', N'San Juan', N'00901'), (N'US', N'GU', N'Hagatna', N'96910'), (N'US', N'VI', N'Charlotte Amalie', N'00802')
	) AS source (CountryCode, StateCode, CityName, PrimaryPostalCode)
	INNER JOIN Location.GeoCity city
		ON city.CountryCode = source.CountryCode AND city.StateCode = source.StateCode AND city.CityName = source.CityName AND city.IsDeleted = 0
)
INSERT Location.GeoPostalCode (GeoPostalCodeId, GeoCityId, PostalCode, SourceCode)
SELECT NEWID(), seed.GeoCityId, seed.PrimaryPostalCode, N'Seed'
FROM SeedZips seed
WHERE NOT EXISTS (
	SELECT 1 FROM Location.GeoPostalCode existing
	WHERE existing.GeoCityId = seed.GeoCityId AND existing.PostalCode = seed.PrimaryPostalCode
);
GO
