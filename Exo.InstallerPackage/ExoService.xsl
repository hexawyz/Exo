<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:wix="http://wixtoolset.org/schemas/v4/wxs" xmlns:util="http://wixtoolset.org/schemas/v4/wxs/util">
    <xsl:output omit-xml-declaration="yes" indent="yes"/>
    <xsl:strip-space elements="*"/>

    <xsl:template match="node()|@*">
        <xsl:copy>
            <xsl:apply-templates select="node()|@*"/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match='wix:Wix/wix:Fragment/wix:DirectoryRef[@Id="INSTALLFOLDER"]/wix:Directory[@Name="Exo.Service"]/wix:Component[wix:File[@Source="SourceDir\Exo.Service.exe"]]'>
        <xsl:element name="wix:Component">
            <xsl:attribute name="Id">
                <xsl:value-of select="@Id"/>
            </xsl:attribute>
            <xsl:attribute name="Guid">
                <xsl:value-of select="@Guid"/>
            </xsl:attribute>

            <xsl:element name="wix:File">
                <xsl:attribute name="Id">
                    <xsl:value-of select="wix:File/@Id"/>
                </xsl:attribute>
                <xsl:attribute name="Source">
                    <xsl:value-of select="wix:File/@Source"/>
                </xsl:attribute>
                <xsl:attribute name="KeyPath">yes</xsl:attribute>
            </xsl:element>

            <xsl:element name="wix:ServiceInstall">
                <xsl:attribute name="Id">ServiceInstaller</xsl:attribute>
                <xsl:attribute name="DisplayName">Exo</xsl:attribute>
                <xsl:attribute name="Name">Exo</xsl:attribute>
                <xsl:attribute name="Description">Exo, the exoskeleton for your Windows PC and devices.</xsl:attribute>
                <xsl:attribute name="Start">auto</xsl:attribute>
                <xsl:attribute name="Type">ownProcess</xsl:attribute>
                <xsl:attribute name="ErrorControl">normal</xsl:attribute>
                <xsl:attribute name="Vital">yes</xsl:attribute>
                <xsl:element name="util:ServiceConfig">
                    <xsl:attribute name="FirstFailureActionType">restart</xsl:attribute>
                    <xsl:attribute name="SecondFailureActionType">restart</xsl:attribute>
                    <xsl:attribute name="ThirdFailureActionType">none</xsl:attribute>
                    <xsl:attribute name="RestartServiceDelayInSeconds">60</xsl:attribute>
                </xsl:element>

            </xsl:element>
            <xsl:element name="wix:ServiceControl">
                <xsl:attribute name="Id">ServiceController</xsl:attribute>
                <xsl:attribute name="Name">Exo</xsl:attribute>
                <xsl:attribute name="Start">install</xsl:attribute>
                <xsl:attribute name="Remove">uninstall</xsl:attribute>
                <xsl:attribute name="Stop">uninstall</xsl:attribute>
                <xsl:attribute name="Wait">yes</xsl:attribute>
            </xsl:element>

        </xsl:element>
    </xsl:template>

</xsl:stylesheet>
