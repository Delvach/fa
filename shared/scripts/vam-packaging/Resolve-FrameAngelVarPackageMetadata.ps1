function Get-FrameAngelOptionalMetadataString {
    param(
        [object]$Metadata,
        [string]$PropertyName
    )

    if ($null -eq $Metadata -or [string]::IsNullOrWhiteSpace($PropertyName)) {
        return ""
    }

    $property = $Metadata.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        return ""
    }

    $value = [string]$property.Value
    if ([string]::IsNullOrWhiteSpace($value)) {
        return ""
    }

    return $value.Trim()
}

function Resolve-FrameAngelVarPackageMetadata {
    param(
        [string]$MetadataPath,
        [string]$BasePath,
        [string]$CreatorName,
        [string]$PackageName,
        [string]$DefaultLicenseType = "FC",
        [string]$DefaultDescription = "",
        [string]$DefaultCredits = "",
        [string]$DefaultInstructions = "",
        [string]$DefaultPromotionalLink = ""
    )

    $resolvedMetadataPath = ""
    $metadata = $null
    if (-not [string]::IsNullOrWhiteSpace($MetadataPath)) {
        $resolvedMetadataPath = if ([System.IO.Path]::IsPathRooted($MetadataPath)) {
            [System.IO.Path]::GetFullPath($MetadataPath)
        }
        else {
            [System.IO.Path]::GetFullPath((Join-Path $BasePath $MetadataPath))
        }

        if (-not (Test-Path -LiteralPath $resolvedMetadataPath)) {
            throw "Var package metadata config not found: $resolvedMetadataPath"
        }

        $metadata = Get-Content -LiteralPath $resolvedMetadataPath -Raw | ConvertFrom-Json
    }

    $resolvedLicenseType = Get-FrameAngelOptionalMetadataString -Metadata $metadata -PropertyName "licenseType"
    if ([string]::IsNullOrWhiteSpace($resolvedLicenseType)) {
        if ([string]::IsNullOrWhiteSpace($DefaultLicenseType)) {
            $resolvedLicenseType = "FC"
        }
        else {
            $resolvedLicenseType = $DefaultLicenseType.Trim()
        }
    }

    if ([string]::IsNullOrWhiteSpace($CreatorName)) {
        $resolvedCreatorName = Get-FrameAngelOptionalMetadataString -Metadata $metadata -PropertyName "creatorName"
    }
    else {
        $resolvedCreatorName = $CreatorName.Trim()
    }

    if ([string]::IsNullOrWhiteSpace($PackageName)) {
        $resolvedPackageName = Get-FrameAngelOptionalMetadataString -Metadata $metadata -PropertyName "packageName"
    }
    else {
        $resolvedPackageName = $PackageName.Trim()
    }

    $resolvedDescription = Get-FrameAngelOptionalMetadataString -Metadata $metadata -PropertyName "description"
    if ([string]::IsNullOrWhiteSpace($resolvedDescription)) {
        if ([string]::IsNullOrWhiteSpace($DefaultDescription)) {
            $resolvedDescription = ""
        }
        else {
            $resolvedDescription = $DefaultDescription.Trim()
        }
    }

    $resolvedCredits = Get-FrameAngelOptionalMetadataString -Metadata $metadata -PropertyName "credits"
    if ([string]::IsNullOrWhiteSpace($resolvedCredits)) {
        if ([string]::IsNullOrWhiteSpace($DefaultCredits)) {
            $resolvedCredits = ""
        }
        else {
            $resolvedCredits = $DefaultCredits.Trim()
        }
    }

    $resolvedInstructions = Get-FrameAngelOptionalMetadataString -Metadata $metadata -PropertyName "instructions"
    if ([string]::IsNullOrWhiteSpace($resolvedInstructions)) {
        if ([string]::IsNullOrWhiteSpace($DefaultInstructions)) {
            $resolvedInstructions = ""
        }
        else {
            $resolvedInstructions = $DefaultInstructions.Trim()
        }
    }

    $resolvedPromotionalLink = Get-FrameAngelOptionalMetadataString -Metadata $metadata -PropertyName "promotionalLink"
    if ([string]::IsNullOrWhiteSpace($resolvedPromotionalLink)) {
        if ([string]::IsNullOrWhiteSpace($DefaultPromotionalLink)) {
            $resolvedPromotionalLink = ""
        }
        else {
            $resolvedPromotionalLink = $DefaultPromotionalLink.Trim()
        }
    }

    if ([string]::IsNullOrWhiteSpace($resolvedCreatorName)) {
        throw "Resolved var package metadata is missing creatorName."
    }

    if ([string]::IsNullOrWhiteSpace($resolvedPackageName)) {
        throw "Resolved var package metadata is missing packageName."
    }

    return [ordered]@{
        metadataConfigPath = $resolvedMetadataPath
        licenseType = $resolvedLicenseType
        creatorName = $resolvedCreatorName
        packageName = $resolvedPackageName
        description = $resolvedDescription
        credits = $resolvedCredits
        instructions = $resolvedInstructions
        promotionalLink = $resolvedPromotionalLink
    }
}

function New-FrameAngelVarPackageMetaObject {
    param([hashtable]$ResolvedMetadata)

    if ($null -eq $ResolvedMetadata) {
        throw "Resolved metadata cannot be null."
    }

    $meta = [ordered]@{
        licenseType = [string]$ResolvedMetadata.licenseType
        creatorName = [string]$ResolvedMetadata.creatorName
        packageName = [string]$ResolvedMetadata.packageName
        description = [string]$ResolvedMetadata.description
    }

    foreach ($optionalField in @("credits", "instructions", "promotionalLink")) {
        $value = [string]$ResolvedMetadata[$optionalField]
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $meta[$optionalField] = $value.Trim()
        }
    }

    return $meta
}
