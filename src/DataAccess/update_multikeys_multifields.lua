local begin = 1

local i = 1
while KEYS[i] do
	local key = KEYS[i]
	local lockId = ARGV[i]
	if not lockId then
		return -1
	else
		local savedLockId = redis.call('HGET', key, '_lockId')
		if savedLockId ~= lockId then
			-- todo to be removed, cause string concant too expensive
			return "failed.. key:" .. key .. " savedLockId:" .. savedLockId .. " inputLockId:" .. lockId
		end	
	end
	i = i + 1
end

local keyPos = 1
while ARGV[i] do
	local fieldsNum = ARGV[i]
	i = i+1
	for j=1,fieldsNum do
		local fieldName = ARGV[i]
		local fieldValue = ARGV[i+1]
		redis.call('HSET', KEYS[keyPos], fieldName, fieldValue)
		i = i + 2
	end
	keyPos = keyPos + 1
end

return 0
