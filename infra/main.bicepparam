using './main.bicep'

param location = 'canadacentral'
param environmentName = 'dev'
param workloadName = 'directory-generator'
param appService = {
	skuName: 'B1'
	environment: {
		apiClientId: '<api-client-id>'
		swaggerClientId: '<swagger-client-id>'
	}
}
